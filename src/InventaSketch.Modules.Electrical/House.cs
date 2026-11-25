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
            ("oneway", "One-way switch", "Ausschalter"), ("twoway", "Two-way switch", "Wechselschalter"), ("push", "Push button", "Taster"));
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
                Views.Plan(10, 10, Socket),
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
            { Pins = Fixed(new Pin("feed", 0, 0.5)), Params = new[] { Gangs, Protected, Height(300) }, Labeled = false, Installation = true };

            yield return new ElecType("elec.house.switch", T("Light switch", "Lichtschalter"), Cat,
                new[] { "light switch", "switch", "two-way", "push button", "schalter", "lichtschalter", "ausschalter", "wechselschalter", "taster", "installation" },
                Views.Plan(10, 10, SwitchPlan),
                Views.Section(8, 8, (p, o) =>
                {
                    Plate(p, 8, 8);
                    p.Rect(-2, -2.6, 2, 2.6, null, "rocker");
                    if (o.Str("kind", "oneway") == "twoway") p.Line(-2, 0, 2, 0, S.Thin, "rocker");
                }),
                Views.Schematic(10, 10, SwitchPlan))
            {
                Pins = Fixed(new Pin("feed", 0, 0.5)), Params = new[] { SwitchKind, Height(1050) }, Labeled = false, Installation = true,
                Extra = o => new[] { Cycle(o, "elec.house.switch.next", T("Next switch type", "Nächste Schalterart"), SwitchKind) },
            };

            yield return new ElecType("elec.house.light", T("Luminaire / light outlet", "Leuchte / Leuchtenauslass"), Cat,
                new[] { "luminaire", "light", "lamp", "ceiling light", "leuchte", "lampe", "deckenleuchte", "auslass", "installation" },
                Views.Plan(10, 10, LightPlan),
                Views.Section(12, 4, (p, o) =>
                {
                    // Ceiling luminaire seen from the side: canopy, body, light cone hinted.
                    p.Rect(-1.5, -2, 1.5, -1.2, null, "canopy");
                    p.Poly(null, true, "body", -6, -1.2, 6, -1.2, 4, 2, -4, 2);
                }),
                Views.Schematic(10, 10, LightPlan))
            { Pins = Fixed(Quadrants), Labeled = false, Params = new[] { Height(2400) }, Installation = true };

            yield return new ElecType("elec.house.junction", T("Junction box", "Abzweigdose"), Cat,
                new[] { "junction box", "box", "abzweigdose", "verteilerdose", "dose", "installation" },
                Views.Plan(5, 5, JunctionPlan),
                Views.Section(5, 5, (p, o) => { Plate(p, 5, 5); p.Dot(0, 0, 0.6, "center"); }),
                Views.Schematic(5, 5, JunctionPlan))
            { Pins = Fixed(Quadrants), Labeled = false, Params = new[] { Height(2200) }, Installation = true };

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
                Params = new[] { Height(1100) }, Installation = true,
            };

            yield return new ElecType("elec.sld.busbar", T("Busbar (single-line)", "Sammelschiene (einpolig)"), Cat,
                new[] { "busbar", "bus bar", "single-line", "sammelschiene", "schiene", "einpolig", "übersichtsschaltplan" },
                Views.Symbol(40, 4, (p, o) => p.Line(-20, 0, 20, 0, S.Bus, "busbar")))
            { Pins = BusbarPins, Uniform = false, RefPrefix = "W", LabelSide = LabelSide.Right };

            yield return new ElecType("elec.sld.feeder", T("Feeder (single-line)", "Abgang (einpolig)"), Cat,
                new[] { "feeder", "outgoing", "single-line", "abgang", "abzweig", "einpolig", "stromkreis" },
                Views.Symbol(10, 30, Feeder))
            { Pins = Fixed(new Pin("bus", 0, -0.5), new Pin("load", 0, 0.5)), Params = new[] { FeederDevice }, Uniform = false, RefPrefix = "Q", LabelSide = LabelSide.Right };

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
            { Pins = Fixed(TwoH().ToArray()), Params = new[] { MeterLabel }, RefPrefix = "P" };

            yield return new CableRouteType();
        }

        private static IReadOnlyList<Pin> BusbarPins(SketchObject o)
            => Enumerable.Range(0, 5).Select(i => new Pin("tap" + N(i), -0.5 + i * 0.25, 0)).ToList();

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

        private static void SwitchPlan(Pen p, SketchObject o)
        {
            var kind = o.Str("kind", "oneway");
            p.Round(0, 0, 1.4, null, "body");
            p.Line(0, 1.4, 0, 5, null, "stem");
            if (kind == "push") { p.Round(0, 0, 0.6, S.Black, "push"); return; }
            var c = 1.4 * Math.Sqrt(0.5);
            p.Poly(S.Sym, false, "lever", c, -c, 3.4, -3.4, 4.4, -2.4);
            if (kind == "twoway") p.Poly(S.Sym, false, "lever", -c, c, -3.4, 3.4, -4.4, 2.4);
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

    /// <summary>Cable route in a floor plan with optional conductor count marks (oblique strokes).</summary>
    internal sealed class CableRouteType : ElecPointType
    {
        /// <summary>Corners can be inserted, ends extended and drawn on (<see cref="Polylines"/>).</summary>
        public override bool InsertablePoints => true;

        private static readonly ParamDef Conductors = Int("conductors", T("Conductors", "Adern"), 3, 0, 12);

        public CableRouteType() : base("elec.house.cable", T("Cable route", "Leitungsführung"), House.Cat, new ViewDef("plan", T("Floor plan", "Installationsplan")) { Icon = IconIds.Plan },
            "cable", "cable route", "line", "installation", "leitung", "kabel", "leitungsführung", "nym") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-20, 0), new Vec2(20, 0) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { Conductors };
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
