using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>
    /// Installation devices in the rooms (socket outlet, switch, light outlet, appliance connection) as plan symbols in
    /// the manner of IEC 60617-11 (drawn independently), each with its room and the designation of the protective device
    /// that feeds its circuit ("-F3"): the data of the house installation list and the circuit plan. Each is a load of its
    /// circuit with optional values (power, current, voltage, power factor) and a phase (<see cref="SwValues"/>).
    /// </summary>
    internal sealed class HouseType : ObjectType, IValueType, IValueLinks
    {
        public const string SocketId = "sw.house.socket";
        public const string SwitchId = "sw.house.switch";
        public const string LightId = "sw.house.light";
        public const string ApplianceId = "sw.house.appliance";

        private readonly string _id;
        private readonly I18nText _name;
        private readonly string[] _keywords;
        private readonly ParamDef? _kind;
        private readonly Action<Pen, SketchObject> _draw;

        public static readonly ParamDef FeedParam = Pm.Text("feed", "Circuit (fed from, e.g. -F3)", "Stromkreis (gespeist von, z. B. -F3)");
        public static readonly ParamDef DeviceRef = Pm.Text("ref", "Designation (optional)", "Kennzeichen (optional)");

        private HouseType(string id, I18nText name, ParamDef? kind, Action<Pen, SketchObject> draw, params string[] keywords)
        {
            _id = id; _name = name; _kind = kind; _draw = draw; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => SwCategories.House;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override Size2 DefaultSize => new(12, 12);
        public override bool PreferUniformScale => true;
        public override IReadOnlyList<ViewDef> Views => new[] { new ViewDef("plan", new I18nText("Installation plan", "Installationsplan")) { Icon = IconIds.Plan } };

        public override IReadOnlyList<ParamDef> Parameters
        {
            get
            {
                var list = new List<ParamDef>();
                if (_kind is not null) list.Add(_kind);
                list.Add(Pm.Room);
                list.Add(FeedParam);
                list.Add(DeviceRef);
                list.Add(SwValues.LoadPhase);
                list.AddRange(ValueSpec.Params);
                return list;
            }
        }

        internal SwValueSpec ValueSpec => SwValues.For(this);
        public IReadOnlyList<VarDef> Variables => ValueSpec.Variables;
        public IReadOnlyList<Relation> Relations => ValueSpec.Relations;
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx) => SwValues.Links(o, ValueSpec, ctx);

        public ParamDef? KindParam => _kind;

        /// <summary>Display name of the device as placed ("Double socket outlet", "Two-way switch").</summary>
        public string Describe(SketchObject o, Lang lang)
        {
            if (_kind is null) return _name.Get(lang);
            var v = o.Str(_kind.Key, _kind.Default?.Text ?? "");
            return (_kind.Options ?? Array.Empty<ParamOption>()).FirstOrDefault(x => x.Value == v)?.Label.Get(lang) ?? _name.Get(lang);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var pen = new Pen(o.Size, new Size2(12, 12));
            _draw(pen, o);
            // Circuit (feeding device) at the top left, small.
            var feed = o.Str("feed", "").Trim();
            if (feed.Length > 0) pen.Text(-5.8, -5.0, Pen.Fit(feed, 2.0, 5.4).Text, Pen.Fit(feed, 2.0, 5.4).Size, anchor: TextAnchor.Start, tag: SwTags.Label);
            // A phase chosen for the load itself, small at the top right.
            var phase = o.Str(SwValues.PhaseKey, SwValues.FromCircuit);
            if (phase != SwValues.FromCircuit) pen.Text(5.8, -5.0, phase == SwValues.ThreePhase ? "3~" : phase, 2.0, anchor: TextAnchor.End, tag: SwTags.Label);
            return pen.Items;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Port, new Vec2(0, o.Size.H / 2), "c");
        }

        public override PortConnection? Connection(SketchObject o, Anchor port) => port.Id is null ? null : Lines.ConnectionFor(PortMedia.ElectricInstallation);

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            if (_kind is not null)
                foreach (var a in ActionChoices.FromParam(o, "sw." + _kind.Key, _kind, _kind.Label, "param", null)) yield return a;
            foreach (var a in ActionChoices.FromParam(o, "sw." + SwValues.PhaseKey, SwValues.LoadPhase, SwValues.LoadPhase.Label, "param", null)) yield return a;
            yield return ContextAction.WithInput("sw.room", new I18nText("Room …", "Raum …"), IconIds.Home, new I18nText("Room", "Raum"), o.Str("room", ""),
                (x, t) => x.WithParam("room", t.Trim())) with { Group = "label" };
            yield return ContextAction.WithInput("sw.feed", new I18nText("Circuit …", "Stromkreis …"), IconIds.Name, new I18nText("Circuit (fed from)", "Stromkreis (gespeist von)"),
                o.Str("feed", ""), (x, t) => x.WithParam("feed", t.Trim())) with { Group = "label" };
        }

        // ------------------------------------------------------------------ the four types

        private static readonly Style Line = Style.Visible;
        private static readonly Style Thin = Style.ThinLine;

        public static IEnumerable<HouseType> Types()
        {
            var gangs = Pm.Choice("gangs", "Gangs", "Fach", "1", ("1", "Single socket outlet", "Einfachsteckdose"), ("2", "Double socket outlet", "Zweifachsteckdose"), ("3", "Triple socket outlet", "Dreifachsteckdose"));
            yield return new HouseType(SocketId, new I18nText("Socket outlet (room/circuit)", "Steckdose (Raum/Stromkreis)"), gangs, (p, o) =>
            {
                // Semicircle open towards the wall (bottom), protective contact line across the top.
                p.Line(0, 6, 0, 2, Line, SwTags.Symbol);
                p.Arc(0, 2, 4.2, 180, 180, Line, SwTags.Symbol);
                p.Line(-4.2, 2, 4.2, 2, Line, SwTags.Symbol);
                p.Line(-3.6, -3.2, 3.6, -3.2, Line, SwTags.Symbol);
                var n = o.Str("gangs", "1");
                if (n != "1") p.Text(4.4, -4.6, n, 2.6, bold: true, anchor: TextAnchor.Start, tag: SwTags.Symbol);
            }, "socket", "steckdose", "schuko", "outlet", "doppelsteckdose");

            var switchKind = Pm.Choice("kind", "Switch type", "Schalterart", "oneway",
                ("oneway", "One-way switch", "Ausschalter"), ("twoway", "Two-way switch", "Wechselschalter"),
                ("intermediate", "Intermediate switch", "Kreuzschalter"), ("push", "Push button", "Taster"), ("dimmer", "Dimmer", "Dimmer"));
            yield return new HouseType(SwitchId, new I18nText("Light switch (room/circuit)", "Lichtschalter (Raum/Stromkreis)"), switchKind, (p, o) =>
            {
                var kind = o.Str("kind", "oneway");
                p.Line(0, 6, 0, 3.6, Line, SwTags.Symbol);
                p.Circle(0, 2, 1.6, kind == "push" ? Line : Line with { Fill = Fill.Solid("#000000") }, SwTags.Symbol);
                if (kind == "push") { p.Circle(0, 2, 0.6, Sx.Solid("#000000"), SwTags.Symbol); return; }
                void Arm(double sx)
                {
                    p.Line(sx * 1.1, 0.9, sx * 4.2, -4, Line, SwTags.Symbol);
                    p.Line(sx * 4.2, -4, sx * 5.4, -3.2, Line, SwTags.Symbol);
                }
                Arm(1);
                if (kind is "twoway" or "intermediate") Arm(-1);
                if (kind == "intermediate")
                {
                    p.Line(-1.1, 3.1, -4.2, 5.4, Line, SwTags.Symbol);
                    p.Line(1.1, 3.1, 4.2, 5.4, Line, SwTags.Symbol);
                }
                if (kind == "dimmer") p.Poly(Thin, false, SwTags.Symbol, (-4.5, -1.5), (-2.5, -4.5), (-0.5, -1.5));
            }, "switch", "schalter", "lichtschalter", "ausschalter", "wechselschalter", "kreuzschalter", "taster", "dimmer");

            var lightKind = Pm.Choice("kind", "Light outlet", "Leuchtenauslass", "ceiling", ("ceiling", "Ceiling light outlet", "Deckenauslass"), ("wall", "Wall light outlet", "Wandauslass"));
            yield return new HouseType(LightId, new I18nText("Light outlet (room/circuit)", "Leuchtenauslass (Raum/Stromkreis)"), lightKind, (p, o) =>
            {
                var wall = o.Str("kind", "ceiling") == "wall";
                var cy = wall ? 0 : -0.5;
                p.Circle(0, cy, 4.6, Line, SwTags.Symbol);
                p.Line(-3.25, cy - 3.25, 3.25, cy + 3.25, Line, SwTags.Symbol);
                p.Line(-3.25, cy + 3.25, 3.25, cy - 3.25, Line, SwTags.Symbol);
                p.Line(0, cy + 4.6, 0, 6, Line, SwTags.Symbol);
                if (wall) p.Line(-5.5, 5.4, 5.5, 5.4, Line, SwTags.Symbol);
            }, "light", "lamp", "leuchte", "auslass", "deckenauslass", "wandauslass", "brennstelle");

            var appliance = Pm.Choice("kind", "Appliance", "Gerät", "cooker",
                ("cooker", "Cooker connection", "Herdanschluss"), ("washer", "Washing machine", "Waschmaschine"), ("dryer", "Tumble dryer", "Wäschetrockner"),
                ("dishwasher", "Dishwasher", "Geschirrspüler"), ("heater", "Water heater", "Durchlauferhitzer"), ("generic", "Appliance", "Gerät"));
            yield return new HouseType(ApplianceId, new I18nText("Appliance connection (room/circuit)", "Geräteanschluss (Raum/Stromkreis)"), appliance, (p, o) =>
            {
                p.Line(0, 6, 0, 4, Line, SwTags.Symbol);
                p.Rect(-4.6, -4.6, 9.2, 8.6, Line, 0, SwTags.Symbol);
                switch (o.Str("kind", "cooker"))
                {
                    case "cooker":
                        foreach (var (x, y) in new[] { (-2.0, -2.4), (2.0, -2.4), (-2.0, 1.6), (2.0, 1.6) }) p.Circle(x, y, 1.2, Thin, SwTags.Symbol);
                        break;
                    case "washer":
                        p.Circle(0, -0.3, 2.9, Thin, SwTags.Symbol);
                        break;
                    case "dryer":
                        p.Circle(0, -0.3, 2.9, Thin, SwTags.Symbol);
                        p.Circle(0, -0.3, 1.4, Thin, SwTags.Symbol);
                        break;
                    case "dishwasher":
                        for (var y = -2.6; y <= 2.0; y += 1.5) p.Line(-3, y, 3, y, Thin, SwTags.Symbol);
                        break;
                    case "heater":
                        p.Poly(Thin, false, SwTags.Symbol, (-3, 1.5), (-2, -2.5), (-1, 1.5), (0, -2.5), (1, 1.5), (2, -2.5), (3, 1.5));
                        break;
                    default:
                        p.Text(0, -0.3, "~", 4, tag: SwTags.Symbol);
                        break;
                }
            }, "appliance", "gerät", "herd", "cooker", "waschmaschine", "geschirrspüler", "trockner", "durchlauferhitzer", "anschlussdose");
        }
    }
}
