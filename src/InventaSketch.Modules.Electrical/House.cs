using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// House installation floor-plan symbols (drawn independently following common DIN/EN practice) and single-line
    /// diagram elements.
    /// </summary>
    internal static class House
    {
        public const string Cat = "elec.house";

        private static readonly ParamDef Gangs = Int("gangs", T("Number of outlets", "Anzahl Steckdosen"), 1, 1, 4);
        private static readonly ParamDef Protected = Flag("protected", T("Protective contact (earthed)", "Schutzkontakt"), true);
        private static readonly ParamDef SwitchKind = Choice("kind", T("Kind", "Art"), "oneway",
            ("oneway", "One-way switch", "Ausschalter"), ("twoway", "Two-way switch", "Wechselschalter"),
            ("intermediate", "Intermediate switch", "Kreuzschalter"), ("push", "Push button", "Taster"), ("dimmer", "Dimmer", "Dimmer"));

        // Room and circuit (optional): the same parameters as on the switchgear room devices, so the switchgear reports (house
        // installation list, circuit plan) and circuit values (operating current I_B and power of the feeding device) read
        // these symbols too. "feed" names the protective device that feeds the circuit ("-F3").
        private static readonly ParamDef Room = Text("room", T("Room", "Raum"), "");
        private static readonly ParamDef Feed = Text("feed", T("Circuit (fed from, e.g. -F3)", "Stromkreis (gespeist von, z. B. -F3)"), "");

        /// <summary>Phase of a single-phase load: from its circuit (default), L1, L2 or L3 (read by the switchgear circuit values).</summary>
        private static readonly ParamDef Phase = Choice("phase", T("Phase", "Phase"), "circuit",
            ("circuit", "From the circuit", "Vom Stromkreis"), ("L1", "L1", "L1"), ("L2", "L2", "L2"), ("L3", "L3", "L3"));
        private static readonly ParamDef FeederDevice = Choice("device", T("Protective device", "Schutzorgan"), "breaker",
            ("breaker", "Circuit breaker", "Leistungsschalter"), ("fuse", "Fuse", "Sicherung"), ("none", "None", "Ohne"));
        private static readonly ParamDef MeterLabel = Text("label", T("Label", "Beschriftung"), "kWh");

        private static readonly Pin[] Quadrants = { new("n", 0, -0.5), new("e", 0.5, 0), new("s", 0, 0.5), new("w", -0.5, 0) };

        /// <summary>Mounting height in mm (bottom edge above the finished floor), used by the section view (<see cref="InstallationViews"/>).</summary>
        private static ParamDef Height(double def) => new(InstallationViews.HeightKey, T("Mounting height (bottom edge above floor)", "Montagehöhe (Unterkante über OKFF)"), ParamKind.Number)
        {
            Default = def, Min = -5000, Max = 30000, Step = 10, Unit = "mm",
        };

        /// <summary>Front plate of a flush-mounted device seen on the wall (section view).</summary>
        private static void Plate(Pen p, double w, double h) => p.Rect(-w / 2, -h / 2, w / 2, h / 2, null, "plate");

        public static IEnumerable<ObjectType> Types()
        {
            yield return new ElecType("elec.house.socket", T("Socket outlet", "Steckdose"), Cat,
                new[] { "socket outlet", "outlet", "receptacle", "socket", "steckdose", "schutzkontakt", "schuko", "doppelsteckdose", "installation" },
                Views.Plan(10, 10, Circuit(Socket)),
                Views.Section(8, 8, (p, o) =>
                {
                    var n = Count(o, "gangs", 1, 1, 4);
                    Plate(p, 8, 8);
                    p.Circle(0, 0, 2.6, null, "insert");
                    p.Dot(-1, 0, 0.45, "pin");
                    p.Dot(1, 0, 0.45, "pin");
                    if (n > 1) p.Text(3.4, -2.2, N(n), 1.8, TextAnchor.End, false, "count");
                }),
                Views.Schematic(10, 10, Socket))
            {
                Pins = Fixed(new Pin("feed", 0, 0.5)), Params = new[] { Gangs, Protected, Height(300), Room, Feed, Phase }.Concat(ElecValues.LoadParams).ToList(), Labeled = false, Installation = true,
                Variables = ElecValues.LoadVariables, Relations = ElecValues.PowerRelations, Net = _ => new NetSpec { Sink = ("U", "I") },
                Checks = new[] { ElecValues.RcdAdditional }, Extra = o => CircuitActions(o, true),
            };

            yield return new ElecType("elec.house.switch", T("Light switch", "Lichtschalter"), Cat,
                new[] { "light switch", "switch", "two-way", "push button", "schalter", "lichtschalter", "ausschalter", "wechselschalter", "taster", "installation" },
                Views.Plan(10, 10, Circuit(SwitchPlan)),
                Views.Section(8, 8, (p, o) =>
                {
                    Plate(p, 8, 8);
                    var kind = o.Str("kind", "oneway");
                    if (kind == "dimmer")
                    {
                        // Rotary knob with its pointer.
                        p.Circle(0, 0, 2.4, null, "knob");
                        p.Line(0, -0.6, 0, -2.0, S.Thin, "knob");
                        return;
                    }
                    p.Rect(-2, -2.6, 2, 2.6, null, "rocker");
                    if (kind is "twoway" or "intermediate") p.Line(-2, 0, 2, 0, S.Thin, "rocker");
                    if (kind == "intermediate") p.Line(0, -2.6, 0, 2.6, S.Thin, "rocker");
                    if (kind == "push") p.Circle(0, 0, 0.8, S.Thin, "rocker");
                }),
                Views.Schematic(10, 10, SwitchPlan))
            {
                Pins = Fixed(new Pin("feed", 0, 0.5)), Params = new[] { SwitchKind, Height(1050), Room, Feed }, Labeled = false, Installation = true, Net = _ => NetSpec.Open("feed"),
                Extra = o => Choice(o, "elec.house.switch", T("Switch type", "Schalterart"), SwitchKind, SwitchPreview).Concat(CircuitActions(o, false)),
            };

            yield return new ElecType("elec.house.light", T("Luminaire / light outlet", "Leuchte / Leuchtenauslass"), Cat,
                new[] { "luminaire", "light", "lamp", "ceiling light", "leuchte", "lampe", "deckenleuchte", "auslass", "installation" },
                Views.Plan(10, 10, Circuit(LightPlan)),
                Views.Section(12, 4, (p, o) =>
                {
                    // Ceiling luminaire seen from the side: canopy, body, light cone hinted.
                    p.Rect(-1.5, -2, 1.5, -1.2, null, "canopy");
                    p.Poly(null, true, "body", -6, -1.2, 6, -1.2, 4, 2, -4, 2);
                }),
                Views.Schematic(10, 10, LightPlan))
            {
                Pins = Fixed(Quadrants), Labeled = false, Params = new[] { Height(2400), Room, Feed, Phase }.Concat(ElecValues.LoadParams).ToList(), Installation = true,
                Variables = ElecValues.LoadVariables, Relations = ElecValues.PowerRelations, Net = _ => QuadrantNode(("U", "I")), Extra = o => CircuitActions(o, true),
            };

            yield return new ElecType("elec.house.junction", T("Junction box", "Abzweigdose"), Cat,
                new[] { "junction box", "box", "abzweigdose", "verteilerdose", "dose", "installation" },
                Views.Plan(5, 5, JunctionPlan),
                Views.Section(5, 5, (p, o) => { Plate(p, 5, 5); p.Dot(0, 0, 0.6, "center"); }),
                Views.Schematic(5, 5, JunctionPlan))
            { Pins = Fixed(Quadrants), Labeled = false, Params = new[] { Height(2200) }, Installation = true, Net = _ => QuadrantNode(null) };

            yield return new ElecType("elec.house.board", T("Distribution board", "Verteiler"), Cat,
                new[] { "distribution board", "consumer unit", "panel", "fuse box", "verteiler", "unterverteilung", "sicherungskasten", "zählerschrank", "installation" },
                Views.Plan(20, 10, BoardPlan),
                Views.Section(20, 30, (p, o) =>
                {
                    // Cabinet front: door with a handle and a viewing strip.
                    p.Rect(-10, -15, 10, 15, null, "body");
                    p.Rect(-8, -11, 8, -7, S.Thin, "strip");
                    p.Line(7, -1.5, 7, 1.5, null, "handle");
                }),
                Views.Schematic(20, 10, BoardPlan))
            {
                Pins = Fixed(new Pin("top", 0, -0.5), new Pin("bottom", 0, 0.5)), Uniform = false, RefPrefix = "UV", LabelSide = LabelSide.Right,
                Params = new[] { Height(1100) }, Installation = true, Net = _ => NetSpec.Short("top", "bottom"),
            };

            yield return new ElecType("elec.sld.busbar", T("Busbar (single-line)", "Sammelschiene (einpolig)"), Cat,
                new[] { "busbar", "bus bar", "single-line", "sammelschiene", "schiene", "einpolig", "übersichtsschaltplan" },
                Views.Symbol(40, 4, (p, o) => p.Line(-20, 0, 20, 0, S.Bus, "busbar")))
            { Pins = BusbarPins, Uniform = false, RefPrefix = "W", LabelSide = LabelSide.Right, Net = o => NetSpec.Short(BusbarPins(o).Select(p => p.Id).ToArray()) };

            yield return new ElecType("elec.sld.feeder", T("Feeder (single-line)", "Abgang (einpolig)"), Cat,
                new[] { "feeder", "outgoing", "single-line", "abgang", "abzweig", "einpolig", "stromkreis" },
                Views.Symbol(10, 30, Feeder))
            {
                Pins = Fixed(new Pin("bus", 0, -0.5), new Pin("load", 0, 0.5)), Params = new[] { FeederDevice }.Concat(ElecValues.DeviceParams).ToList(), Uniform = false,
                RefPrefix = "Q", LabelSide = LabelSide.Right, Variables = ElecValues.DeviceVariables, Marking = ElecValues.DeviceMarking,
                Net = ElecValues.Device("bus", "load"), Checks = new[] { ElecValues.Coordination, ElecValues.NotReducible },
            };

            yield return new ElecType("elec.sld.meter", T("Energy meter", "Energiezähler"), Cat,
                new[] { "meter", "energy meter", "kwh", "zähler", "stromzähler", "energiezähler", "einpolig" },
                Views.Symbol(20, 10, (p, o) =>
                {
                    p.Line(-10, 0, -6, 0, null, "lead");
                    p.Line(6, 0, 10, 0, null, "lead");
                    p.Rect(-6, -4, 6, 4, null, "body");
                    p.Line(-6, -1.6, 6, -1.6, S.Thin, "body");
                    var label = o.Str("label", "kWh");
                    if (label.Length > 0) p.Text(0, 1.3, label, Math.Min(3, 10.0 / (label.Length * 0.55)), TextAnchor.Middle, false, "name");
                }))
            { Pins = Fixed(TwoH().ToArray()), Params = new[] { MeterLabel }, RefPrefix = "P", Net = _ => NetSpec.Short("1", "2") };

            yield return new CableRouteType();
        }

        /// <summary>A plan element whose four connection points are one node (junction box, luminaire), optionally a single-line load.</summary>
        private static NetSpec QuadrantNode((string U, string I)? sink)
        {
            var n = new NetSpec { Sink = sink };
            n.Shorts.Add(Quadrants.Select(p => p.Id).ToArray());
            return n;
        }

        private static IReadOnlyList<Pin> BusbarPins(SketchObject o)
            => Enumerable.Range(0, 5).Select(i => new Pin("tap" + N(i), -0.5 + i * 0.25, 0)).ToList();

        /// <summary>Room, circuit and (for loads) phase typed or chosen directly (pie menu / bar), as on the switchgear room devices.</summary>
        private static IEnumerable<ContextAction> CircuitActions(SketchObject o, bool load)
        {
            if (load)
                foreach (var a in Choice(o, "elec.house.phase", T("Phase", "Phase"), Phase)) yield return a;
            yield return ContextAction.WithInput("elec.house.room", T("Room …", "Raum …"), IconIds.Home, T("Room", "Raum"), o.Str("room", ""),
                (x, t) => x.WithParam("room", t.Trim())) with { Group = "label" };
            yield return ContextAction.WithInput("elec.house.feed", T("Circuit …", "Stromkreis …"), IconIds.Name, T("Circuit (fed from)", "Stromkreis (gespeist von)"),
                o.Str("feed", ""), (x, t) => x.WithParam("feed", t.Trim())) with { Group = "label" };
        }

        /// <summary>
        /// The plan symbol with its circuit ("-F3") small at the top left, outside the symbol (like a designator label), as
        /// installation plans mark the circuit of an outlet. Nothing is added while no circuit is set.
        /// </summary>
        private static Action<Pen, SketchObject> Circuit(Action<Pen, SketchObject> draw) => (p, o) =>
        {
            draw(p, o);
            var feed = o.Str("feed", "").Trim();
            if (feed.Length > 0) p.Text(-5.2, -4.2, feed, 2.0, TextAnchor.End, false, "circuit");
        };

        /// <summary>Option pictogram of a switch kind: its plan symbol, about 6 mm.</summary>
        private static IReadOnlyList<Primitive> SwitchPreview(string kind)
        {
            var p = new Pen(new Size2(6, 6), new Size2(10, 10));
            SwitchPlan(p, kind);
            return p.Items;
        }

        private static void Socket(Pen p, SketchObject o)
        {
            // Cup open to the room side (top); the stem leads to the wall/feed (bottom).
            p.Arc(0, -1.5, 3.5, 3.5, 0, 180, null, "cup");
            p.Line(0, 2, 0, 5, null, "stem");
            if (o.Flag("protected", true)) p.Line(-3.5, -1.5, 3.5, -1.5, null, "earth");
            var n = Count(o, "gangs", 1, 1, 4);
            if (n > 1) p.Text(3.5, -3.5, N(n), 2.5, TextAnchor.Start, false, "count");
        }

        private static void LightPlan(Pen p, SketchObject o)
        {
            p.Circle(0, 0, 5, null, "body");
            var d = 5 * Math.Sqrt(0.5);
            p.Line(-d, -d, d, d, null, "cross");
            p.Line(-d, d, d, -d, null, "cross");
        }

        private static void JunctionPlan(Pen p, SketchObject o)
        {
            p.Circle(0, 0, 2.5, null, "body");
            p.Dot(0, 0, 0.9, "center");
        }

        private static void BoardPlan(Pen p, SketchObject o)
        {
            p.Rect(-10, -5, 10, 5, null, "body");
            p.Rect(-10, 0, 10, 5, S.Hatch(1.2), "fill");
        }

        private static void SwitchPlan(Pen p, SketchObject o) => SwitchPlan(p, o.Str("kind", "oneway"));

        /// <summary>
        /// Switch in the plan: a small circle with a lever per switched direction; two-way adds the opposite lever, intermediate
        /// all four (crossing), push button a dot inside, dimmer the one-way lever with an adjustment wedge beside it.
        /// </summary>
        private static void SwitchPlan(Pen p, string kind)
        {
            p.Round(0, 0, 1.4, null, "body");
            p.Line(0, 1.4, 0, 5, null, "stem");
            if (kind == "push") { p.Round(0, 0, 0.6, S.Black, "push"); return; }
            var c = 1.4 * Math.Sqrt(0.5);
            p.Poly(S.Sym, false, "lever", c, -c, 3.4, -3.4, 4.4, -2.4);
            if (kind is "twoway" or "intermediate") p.Poly(S.Sym, false, "lever", -c, c, -3.4, 3.4, -4.4, 2.4);
            if (kind == "intermediate")
            {
                p.Poly(S.Sym, false, "lever", -c, -c, -3.4, -3.4, -2.4, -4.4);
                p.Poly(S.Sym, false, "lever", c, c, 3.4, 3.4, 2.4, 4.4);
            }
            if (kind == "dimmer") p.Poly(S.Thin, true, "dimmer", -4.6, -1.2, -1.2, -1.2, -1.2, -3.6);
        }

        private static void Feeder(Pen p, SketchObject o)
        {
            switch (o.Str("device", "breaker"))
            {
                case "fuse":
                    p.Line(0, -15, 0, 11, null, "line");
                    p.Rect(-1.6, -5, 1.6, 5, null, "fuse");
                    break;
                case "none":
                    p.Line(0, -15, 0, 11, null, "line");
                    break;
                default:
                    p.Line(0, -15, 0, -4, null, "line");
                    p.Cross(0, -4, 1.0, null, "breaker");
                    p.Line(0, 4, -2.6, -3.4, null, "blade");
                    p.Line(0, 4, 0, 11, null, "line");
                    break;
            }
            p.Head(0, 15, 0, 11, 4, 1.6, "arrow");
        }
    }

    /// <summary>
    /// Cable route in a floor plan with optional conductor count marks (oblique strokes). Optional cable values (round 6):
    /// conductor resistance, voltage drop with cos φ (single-phase, three-phase or DC) and losses, with the check
    /// <c>elec.cable.drop</c> against a permitted drop the user enters (<see cref="AcValues"/>).
    /// </summary>
    internal sealed class CableRouteType : ElecPointType, IValueType, IValueLinks, IValueChecks
    {
        /// <summary>Corners can be inserted, ends extended and drawn on (<see cref="Polylines"/>).</summary>
        public override bool InsertablePoints => true;

        private static readonly ParamDef Conductors = Int("conductors", T("Conductors", "Adern"), 3, 0, 12);

        public CableRouteType() : base("elec.house.cable", T("Cable route", "Leitungsführung"), House.Cat, new ViewDef("plan", T("Floor plan", "Installationsplan")) { Icon = IconIds.Plan },
            "cable", "cable route", "line", "installation", "leitung", "kabel", "leitungsführung", "nym") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { Conductors }.Concat(AcValues.CableParams).ToList();

        public IReadOnlyList<VarDef> Variables => AcValues.CableVariables;
        public IReadOnlyList<Relation> Relations => AcValues.CableRelations;
        /// <summary>The voltage drop and losses of the cable's system (from the conductor count unless chosen), aluminium's resistivity.</summary>
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx) => AcValues.CableLinks(o);
        IEnumerable<ValueCheck> IValueChecks.ValueChecks => new[] { AcValues.CableDrop };
        /// <summary>An installation cable (build "+" at its free ends, <see cref="Building"/>).</summary>
        public override string? LineMedium(SketchObject line) => PortMedia.ElectricInstallation;

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, IReadOnlyList<Vec2> p, RenderContext ctx)
        {
            var list = new List<Primitive> { Prim.Polyline(p, Style.Visible) with { Tag = "cable" } };
            var n = Count(o, "conductors", 3, 0, 12);
            if (n == 0) return list;
            var (mid, dir) = Routing.LongestSegment(p);
            var nrm = dir.Perp;
            // Oblique stroke: rotated 60 degrees against the cable direction.
            var stroke = (nrm * 1.0 + dir * 0.6) * 1.6;
            var marks = n <= 3 ? n : 1;
            for (var i = 0; i < marks; i++)
            {
                var c = mid + dir * ((i - (marks - 1) / 2.0) * 1.4);
                list.Add(Prim.Line(c - stroke, c + stroke, Style.ThinLine with { StrokeWidth = 0.35 }) with { Tag = "conductors" });
            }
            if (n > 3)
            {
                var pos = mid + Routing.UpNormal(dir) * 3.2 + dir * 2.4;
                list.Add(Prim.Text(pos.X, pos.Y, N(n), new TextStyle { Size = 2.5 }) with { Tag = "conductors" });
            }
            return list;
        }
    }
}
