using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>What a device does in the board; drives the exports (circuit plan, board layout, terminal plan).</summary>
    public enum DeviceRole
    {
        /// <summary>Miniature circuit breaker, fused terminal: protects one circuit.</summary>
        Protection,
        /// <summary>Residual current device (RCD).</summary>
        Rcd,
        /// <summary>Combined RCD and circuit breaker (RCBO): protects one circuit.</summary>
        Rcbo,
        /// <summary>Main switch / switch-disconnector.</summary>
        MainSwitch,
        /// <summary>Surge protective device.</summary>
        Spd,
        /// <summary>Energy meter.</summary>
        Meter,
        /// <summary>Contactors, relays, timer relays.</summary>
        Switching,
        /// <summary>Bus actuators, couplers, bus power supplies.</summary>
        Bus,
        /// <summary>DIN-rail power supplies.</summary>
        Supply,
        /// <summary>Push buttons, switches, indicator lamps on the rail.</summary>
        Control,
        /// <summary>Socket outlet on the rail.</summary>
        Socket,
        /// <summary>Terminal blocks.</summary>
        Terminal,
        /// <summary>End clamps, plug-in bridges, comb busbars, blanking plates.</summary>
        Accessory,
        /// <summary>Frequency inverters (a load of its circuit).</summary>
        Drive,
        /// <summary>PLC modules, safety relays.</summary>
        Automation,
    }

    /// <summary>How a column of a device is drawn in the circuit symbol view.</summary>
    internal enum ColumnKind
    {
        Breaker, Disconnector, Contactor, NormallyOpen, NormallyClosed, PushOpen, PushClosed, Changeover, Coil, Fuse, Terminal, Spd, Box,
        /// <summary>Thermal (bimetal) element of an overload relay.</summary>
        Thermal,
        /// <summary>Fuse on the blade of a switch-disconnector.</summary>
        FuseSwitch,
    }

    /// <summary>
    /// One column of connection points of a device: a top pin, a bottom pin and, for changeover contacts, a second bottom
    /// pin (two slots wide). Front views spread the slots evenly over the device width; the symbol view draws one contact
    /// or coil per column.
    /// </summary>
    internal sealed record Column(string? Top, string? Bottom, ColumnKind Kind)
    {
        public string? Bottom2 { get; init; }
        /// <summary>Front view: own vertical position of the pins (fraction of the height from the centre, 0.5 = edge).</summary>
        public double FrontV { get; init; } = 0.5;
        /// <summary>Front view: own horizontal position (normalised, -0.5..0.5); null = the slot centre.</summary>
        public double? FrontU { get; init; }
        /// <summary>Medium of the pins (<see cref="PortMedia"/>).</summary>
        public string Medium { get; init; } = PortMedia.Electric;
        public int Slots => Bottom2 is null ? 1 : 2;
    }

    /// <summary>A connection pin in normalised box coordinates (U, V in [-0.5, 0.5]).</summary>
    internal readonly record struct Pin(string Id, double U, double V, string Medium);

    /// <summary>Data of one DIN-rail device type (front view, circuit symbol, pins, parameters).</summary>
    internal sealed class DeviceSpec
    {
        public DeviceSpec(string id, I18nText name, string category, DeviceRole role, string prefix)
        {
            Id = id; Name = name; Category = category; Role = role; Prefix = prefix;
        }

        public string Id { get; }
        public I18nText Name { get; }
        public string Category { get; }
        public DeviceRole Role { get; }
        /// <summary>Letter code of the designation (IEC 81346-2 ideas): "-F", "-Q", "-K", "-P", "-T", "-S", "-X".</summary>
        public string Prefix { get; }
        public string[] Keywords { get; init; } = Array.Empty<string>();
        /// <summary>Type-specific parameters (designation, circuit, room, feed and switch state are added by role).</summary>
        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        /// <summary>Unscaled front-view size for the parameters (modular width × height).</summary>
        public Func<SketchObject, Size2> Natural { get; init; } = _ => new Size2(Modular.Unit, Modular.DeviceHeight);
        public Func<SketchObject, IReadOnlyList<Column>> Columns { get; init; } = _ => Array.Empty<Column>();
        /// <summary>Front view drawing in design mm (the box of <see cref="Natural"/>, centred).</summary>
        public Action<Pen, SketchObject, IReadOnlyList<double>> Front { get; init; } = (_, _, _) => { };
        /// <summary>Draws the generic screw terminals at the front pins.</summary>
        public bool Screws { get; init; } = true;
        /// <summary>Rating text printed on the device and listed in the exports ("B16", "40 A 30 mA").</summary>
        public Func<SketchObject, string> Rating { get; init; } = _ => "";
        /// <summary>Text inside the box of the symbol view (meters, supplies, bus devices).</summary>
        public Func<SketchObject, string> SymbolText { get; init; } = _ => "";
        /// <summary>Lever with on/off state (circuit breakers, RCDs, switches).</summary>
        public bool Lever { get; init; }
        /// <summary>Dashed mechanical link between the contacts in the symbol view.</summary>
        public bool Linked { get; init; }
        /// <summary>Residual current detection (toroid and IΔ box in the symbol view).</summary>
        public bool Residual { get; init; }
        public IReadOnlyList<string> Companions { get; init; } = Array.Empty<string>();
        /// <summary>Offers the circuit symbol view (accessories have none).</summary>
        public bool HasSymbol { get; init; } = true;
        /// <summary>Extra actions of the type (terminal numbering).</summary>
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }
        /// <summary>Mount anchors at the left and right edge on the centre line (not for a comb busbar above the devices).</summary>
        public bool Mounts { get; init; } = true;
    }

    /// <summary>
    /// DIN-rail device (modular installation device, terminal block, accessory): a box object with a front view that looks
    /// like the real device and an optional circuit symbol view. Pins are <see cref="AnchorRole.Port"/> anchors with a medium
    /// (wires dock there, "+" builds on); in the front view the left and right edges on the rail centre line are
    /// <see cref="AnchorRole.Mount"/> anchors, so devices snap onto the rail grid and side by side.
    /// </summary>
    internal sealed class DeviceType : ObjectType, IValueType, IValueLinks, IValueChecks
    {
        public const string FrontView = "front";
        public const string SymbolView = "symbol";
        private readonly DeviceSpec _spec;
        private readonly IReadOnlyList<ViewDef> _views;

        public DeviceType(DeviceSpec spec)
        {
            _spec = spec;
            // Preferred sizes of the views (default parameters): switching the view keeps the visual scale (SizeForView).
            var probe = new SketchObject { Params = spec.Params.Where(p => p.Default.HasValue).ToDictionary(p => p.Key, p => p.Default!.Value) };
            var views = new List<ViewDef> { new(FrontView, new I18nText("Front view", "Frontansicht")) { Icon = IconIds.Front, Size = NaturalOf(probe, FrontView) } };
            if (spec.HasSymbol) views.Add(new ViewDef(SymbolView, new I18nText("Circuit symbol", "Schaltzeichen")) { Icon = IconIds.Symbol, Size = NaturalOf(probe, SymbolView) });
            _views = views;
        }

        public DeviceSpec Spec => _spec;
        public override string Id => _spec.Id;
        public override I18nText Name => _spec.Name;
        public override string Category => _spec.Category;
        public override IReadOnlyList<string> Keywords => _spec.Keywords;
        public override IReadOnlyList<ViewDef> Views => _views;
        public override string? DefaultView => FrontView;
        public override Size2 DefaultSize => _spec.Natural(Create(Vec2.Zero, null));
        public override IReadOnlyList<string> Companions => _spec.Companions;

        public bool HasCircuit => _spec.Role is not (DeviceRole.Terminal or DeviceRole.Accessory);
        public bool HasRoom => _spec.Role is DeviceRole.Protection or DeviceRole.Rcbo;

        public override IReadOnlyList<ParamDef> Parameters
        {
            get
            {
                var list = new List<ParamDef>(_spec.Params);
                if (_spec.Lever) list.Add(Pm.On);
                if (_spec.Role != DeviceRole.Accessory) list.Add(_spec.Role == DeviceRole.Terminal ? Terminals.StripRef : Pm.Ref);
                if (HasCircuit) { list.Add(Pm.Circuit); list.Add(Pm.Feed); }
                if (HasRoom) list.Add(Pm.Room);
                list.AddRange(ValueSpec.Params);
                return list;
            }
        }

        public override SketchObject Create(Vec2 at, LibraryItem? item = null)
        {
            var parameters = new Dictionary<string, ParamValue>();
            foreach (var p in _spec.Params) if (p.Default.HasValue) parameters[p.Key] = p.Default.Value;
            if (_spec.Lever) parameters["on"] = true;
            if (item?.Params is not null) foreach (var kv in item.Params) parameters[kv.Key] = kv.Value;
            var o = new SketchObject
            {
                Id = Ids.New(),
                Type = Id,
                Transform = new Transform(at.X, at.Y),
                View = item?.View ?? FrontView,
                Params = parameters,
                Text = item?.Text,
                Style = item?.Style,
            };
            return o with { Size = item?.Size ?? NaturalOf(o, ViewOf(o)) };
        }

        // ------------------------------------------------------------------ sizes

        private const double SlotWidth = 8, SymbolHeight = 30, SymbolMargin = 12, ResidualSlot = 8;

        public Size2 NaturalOf(SketchObject o, string view)
        {
            if (view != SymbolView || !_spec.HasSymbol) return _spec.Natural(o);
            var slots = Math.Max(1, _spec.Columns(o).Sum(c => c.Slots));
            return new Size2(SymbolLeft(_spec.Columns(o)) + slots * SlotWidth + (_spec.Residual ? ResidualSlot : 0) + SymbolMargin, SymbolHeight);
        }

        public override Size2? NaturalSize(SketchObject o) => NaturalOf(o, ViewOf(o));

        /// <summary>Room left of the first column of the symbol view for the actuator of a push contact.</summary>
        public static double SymbolLeft(IReadOnlyList<Column> cols) => cols.Any(c => c.Kind is ColumnKind.PushOpen or ColumnKind.PushClosed) ? 5 : 0;

        /// <summary>Modular width in units (TE) of the front view.</summary>
        public double Modules(SketchObject o) => _spec.Natural(o).W / Modular.Unit;

        public override Size2 SizeForView(SketchObject o, string toView, RenderContext ctx)
        {
            var from = NaturalOf(o, ViewOf(o));
            var to = NaturalOf(o, toView);
            if (from.W < Geo.Eps || from.H < Geo.Eps || o.Size.W < Geo.Eps || o.Size.H < Geo.Eps) return to;
            var k = Math.Sqrt(o.Size.W / from.W * (o.Size.H / from.H));
            return new Size2(to.W * k, to.H * k);
        }

        /// <summary>
        /// After a parameter changed: when the natural size changed (more poles, another cross-section) the box follows
        /// with the same scale, keeping its left edge (so the device stays in its place on the rail).
        /// </summary>
        public SketchObject Adapt(SketchObject before, SketchObject after)
        {
            var nb = NaturalOf(before, ViewOf(before));
            var na = NaturalOf(after, ViewOf(after));
            if (nb.W < Geo.Eps || nb.H < Geo.Eps || (Math.Abs(nb.W - na.W) < 1e-9 && Math.Abs(nb.H - na.H) < 1e-9)) return after;
            var size = new Size2(after.Size.W * na.W / nb.W, after.Size.H * na.H / nb.H);
            var dw = (size.W - after.Size.W) / 2;
            var rad = Geo.DegToRad(after.Transform.Rotation);
            var sign = after.Transform.FlipX ? -1 : 1;
            var t = after.Transform with { X = after.Transform.X + Math.Cos(rad) * dw * sign, Y = after.Transform.Y + Math.Sin(rad) * dw * sign };
            return after with { Size = size, Transform = t };
        }

        /// <summary>
        /// After a parameter changed: a number written into a choice parameter (a value entered for I_n, which is bound to
        /// the rating choice) is stored as its text ("20"), so the device shows it; then the box follows (<see cref="Adapt"/>).
        /// </summary>
        public override SketchObject OnParamChanged(SketchObject before, SketchObject after, string key)
        {
            var def = _spec.Params.FirstOrDefault(p => p.Key == key);
            if (def is { Kind: ParamKind.Choice } && after.Params.TryGetValue(key, out var v) && v.Number is double n && !double.IsNaN(n) && !double.IsInfinity(n))
                after = after.WithParam(key, n.ToString("0.###", CultureInfo.InvariantCulture));
            return Adapt(before, after);
        }

        // ------------------------------------------------------------------ element values (SwValues)

        /// <summary>Variables, value parameters and checks of this type (empty for devices without values).</summary>
        internal SwValueSpec ValueSpec => SwValues.For(this);

        public IReadOnlyList<VarDef> Variables => ValueSpec.Variables;
        public IReadOnlyList<Relation> Relations => ValueSpec.Relations;
        public IEnumerable<ValueCheck> ValueChecks => ValueSpec.Checks;
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx) => SwValues.Links(o, ValueSpec, ctx);

        // ------------------------------------------------------------------ pins

        /// <summary>The pins for the object's view, normalised to its box.</summary>
        public IReadOnlyList<Pin> Pins(SketchObject o) => Pins(o, ViewOf(o));

        public IReadOnlyList<Pin> Pins(SketchObject o, string view)
        {
            var cols = _spec.Columns(o);
            var list = new List<Pin>();
            var slots = Math.Max(1, cols.Sum(c => c.Slots));
            if (view == SymbolView)
            {
                var n = NaturalOf(o, view);
                var x0 = -n.W / 2 + SymbolLeft(cols);
                var k = 0;
                foreach (var c in cols)
                {
                    var left = x0 + (k + 0.5) * SlotWidth;
                    var centre = x0 + (k + c.Slots / 2.0) * SlotWidth;
                    if (c.Top is not null) list.Add(new Pin(c.Top, centre / n.W, -0.5, c.Medium));
                    if (c.Bottom is not null) list.Add(new Pin(c.Bottom, (c.Bottom2 is null ? centre : left) / n.W, 0.5, c.Medium));
                    if (c.Bottom2 is not null) list.Add(new Pin(c.Bottom2, (left + SlotWidth) / n.W, 0.5, c.Medium));
                    k += c.Slots;
                }
                return list;
            }
            var frontSlots = FrontSlots(cols);
            var j = 0;
            foreach (var c in cols)
            {
                double U(int slot) => -0.5 + (slot + 0.5) / frontSlots;
                if (c.FrontU is double fu)
                {
                    // A pin with its own place (the PE terminal of a surge arrester, the levels of a terminal).
                    if (c.Top is not null) list.Add(new Pin(c.Top, fu, -c.FrontV, c.Medium));
                    if (c.Bottom is not null) list.Add(new Pin(c.Bottom, fu, c.FrontV, c.Medium));
                    continue;
                }
                var centre = -0.5 + (j + c.Slots / 2.0) / frontSlots;
                if (c.Top is not null) list.Add(new Pin(c.Top, centre, -c.FrontV, c.Medium));
                if (c.Bottom is not null) list.Add(new Pin(c.Bottom, c.Bottom2 is null ? centre : U(j), c.FrontV, c.Medium));
                if (c.Bottom2 is not null) list.Add(new Pin(c.Bottom2, U(j + 1), c.FrontV, c.Medium));
                j += c.Slots;
            }
            return list;
        }

        /// <summary>Slots over the front width: every column without an own place.</summary>
        public static int FrontSlots(IReadOnlyList<Column> cols) => Math.Max(1, cols.Where(c => c.FrontU is null).Sum(c => c.Slots));

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            foreach (var p in Pins(o)) yield return Anchor.Point(AnchorRole.Port, new Vec2(p.U * o.Size.W, p.V * o.Size.H), p.Id);
            if (ViewOf(o) == SymbolView || !_spec.Mounts) yield break;
            // On the rail centre line: neighbours and rail grid points snap here.
            yield return Anchor.Point(AnchorRole.Mount, new Vec2(-o.Size.W / 2, 0), "left");
            yield return Anchor.Point(AnchorRole.Mount, new Vec2(o.Size.W / 2, 0), "right");
        }

        /// <summary>Wires at the screw terminals, bus cables at bus terminals, installation cables at sockets.</summary>
        public override PortConnection? Connection(SketchObject o, Anchor port)
        {
            if (port.Id is null) return null;
            var pin = Pins(o).FirstOrDefault(p => p.Id == port.Id);
            if (pin.Id is null) return null;
            return Lines.ConnectionFor(pin.Medium, Lines.CoreFor(o.Type, pin.Id));
        }

        // ------------------------------------------------------------------ rendering

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var view = ViewOf(o);
            var design = NaturalOf(o, view);
            var pen = new Pen(o.Size, design);
            if (view == SymbolView) Symbols.Draw(pen, o, this);
            else
            {
                var pins = Pins(o, FrontView);
                // Slot centres (design x) for the front drawing.
                var slots = FrontSlots(_spec.Columns(o));
                var xs = Enumerable.Range(0, slots).Select(i => -design.W / 2 + (i + 0.5) * design.W / slots).ToList();
                _spec.Front(pen, o, xs);
                if (_spec.Screws) Fronts.ScrewTerminals(pen, pins);
            }
            return pen.Items;
        }

        // ------------------------------------------------------------------ actions

        public override bool DependsOnNeighbours => true;

        /// <summary>Labelling strips near the device list it: they are redrawn when it changes.</summary>
        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
            => ObjectsNear(ctx.Doc, o, 150).Where(x => x.Type == Labels.StripId).Select(x => x.Id).ToList();

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var def in _spec.Params.Where(p => p.Kind == ParamKind.Choice && p.Options is { Count: > 1 } && !p.Advanced))
            {
                // A multi-pole device carries all phases: no phase choice.
                if (def.Key == SwValues.PhaseKey && SwValues.IsThreePhase(o)) continue;
                foreach (var a in ActionChoices.FromParam(o, "sw." + def.Key, def, def.Label, "param", null))
                {
                    var inner = a.Apply;
                    yield return a with { Apply = x => Adapt(x, inner(x)) };
                }
            }
            if (_spec.Lever)
                yield return new ContextAction("sw.on", new I18nText("Switched on", "Eingeschaltet"), IconIds.Toggle, x => x.WithParam("on", !x.Flag("on", true)))
                { IsActive = o.Flag("on", true), Group = "state" };
            if (_spec.Extra is not null)
                foreach (var a in _spec.Extra(o)) yield return a;
            if (_spec.Role == DeviceRole.Accessory) yield break;
            var current = o.Str("ref", "");
            var prefix = _spec.Prefix;
            yield return ContextAction.WithInput("sw.ref", new I18nText("Designation …", "Kennzeichen …"), IconIds.Name,
                new I18nText("Designation", "Kennzeichen"), current.Length > 0 ? current : prefix + "1", (x, t) => x.WithParam("ref", t.Trim()))
                with { Group = "label" };
        }
    }
}
