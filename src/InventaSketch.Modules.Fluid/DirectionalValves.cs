using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Fluid.Common;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>
    /// Directional control valves as switching-position boxes (2/2, 3/2, 4/2, 4/3, 5/2, 5/3) with actuation symbols
    /// composed by parameters, and the separate actuation symbol. The ports are drawn at the box of the normal (rest)
    /// position: the centre box of 3-position valves, otherwise the box next to the single return spring (right box if
    /// none or both sides have a spring).
    /// </summary>
    internal static class DirectionalValves
    {
        public const string Cat = "fluid.dcv";
        public const string TypeId = "fluid.valve.directional";

        /// <summary>Width of one actuation element zone (design mm).</summary>
        public const double Zone = 5;
        public const double Half = 5;

        public static readonly string[] WaysOptions = { "2/2", "3/2", "4/2", "4/3", "5/2", "5/3" };
        public static readonly string[] Operators = { "none", "manual", "pushbutton", "lever", "pedal", "roller", "spring", "solenoid", "pilot" };
        public static readonly string[] Centres4 = { "closed", "tandem", "float", "open", "pressure" };
        public static readonly string[] Centres5 = { "closed", "float", "pressure" };

        public static readonly ParamDef Ways = Choice("ways", T("Ways / positions", "Wege / Schaltstellungen"), "4/3",
            ("2/2", "2/2-way", "2/2-Wegeventil"), ("3/2", "3/2-way", "3/2-Wegeventil"), ("4/2", "4/2-way", "4/2-Wegeventil"),
            ("4/3", "4/3-way", "4/3-Wegeventil"), ("5/2", "5/2-way", "5/2-Wegeventil"), ("5/3", "5/3-way", "5/3-Wegeventil"));

        public static readonly ParamDef Normal = Choice("normal", T("Normal position (2/2, 3/2)", "Grundstellung (2/2, 3/2)"), "closed",
            ("closed", "Normally closed", "Sperr-Ruhestellung (geschlossen)"), ("open", "Normally open", "Durchfluss-Ruhestellung (offen)"));

        public static readonly ParamDef Centre = Choice("centre", T("Centre position (3 positions)", "Mittelstellung (3 Stellungen)"), "closed",
            ("closed", "Closed centre (all ports blocked)", "Sperrmittelstellung (alle gesperrt)"),
            ("tandem", "Tandem centre (P to T, A and B blocked)", "Umlaufmittelstellung (P nach T, A und B gesperrt)"),
            ("float", "Float / exhausted centre (A and B to T/R)", "Schwimm-/Entlüftungsmittelstellung (A und B nach T/R)"),
            ("open", "Open centre (all ports connected)", "Offene Mittelstellung (alle verbunden)"),
            ("pressure", "Pressurised centre (P to A and B)", "Belüftete Mittelstellung (P nach A und B)"));

        private static ParamDef Actuation(string key, I18nText label, string def) => Choice(key, label, def,
            ("none", "None", "Keine"), ("manual", "Manual (general)", "Muskelkraft (allgemein)"), ("pushbutton", "Push button", "Druckknopf"),
            ("lever", "Lever", "Hebel"), ("pedal", "Pedal", "Pedal"), ("roller", "Roller (mechanical)", "Rolle (mechanisch)"),
            ("spring", "Spring", "Feder"), ("solenoid", "Solenoid", "Elektromagnet"), ("pilot", "Pressure pilot", "Druckansteuerung (Vorsteuerung)"));

        public static readonly ParamDef ActLeft = Actuation("actLeft", T("Actuation left", "Betätigung links"), "solenoid");
        public static readonly ParamDef ActRight = Actuation("actRight", T("Actuation right", "Betätigung rechts"), "solenoid");
        public static readonly ParamDef Centring = Flag("centring", T("Spring centred (3 positions)", "Federzentriert (3 Stellungen)"), true);
        public static readonly ParamDef Detent = Flag("detent", T("Detent (locking)", "Rastung"), false);

        public static readonly ParamDef ActKind = Choice("kind", T("Actuation", "Betätigung"), "pushbutton",
            ("manual", "Manual (general)", "Muskelkraft (allgemein)"), ("pushbutton", "Push button", "Druckknopf"),
            ("lever", "Lever", "Hebel"), ("pedal", "Pedal", "Pedal"), ("roller", "Roller (mechanical)", "Rolle (mechanisch)"),
            ("spring", "Spring", "Feder"), ("solenoid", "Solenoid", "Elektromagnet"), ("pilot", "Pressure pilot", "Druckansteuerung (Vorsteuerung)"),
            ("detent", "Detent", "Rastung"));

        public static IEnumerable<FluidType> Types()
        {
            yield return new FluidType(TypeId, T("Directional control valve", "Wegeventil"), Cat,
                new[]
                {
                    "directional valve", "control valve", "spool valve", "solenoid valve", "2/2", "3/2", "4/2", "4/3", "5/2", "5/3", "way valve",
                    "wegeventil", "magnetventil", "schieberventil", "steuerventil", "ventil",
                },
                new ViewSpec("symbol", T("Circuit symbol", "Schaltzeichen"), IconIds.Symbol, 40, 10, Draw) { DesignFor = DesignOf })
            {
                MediumDefault = "hydraulic", Params = new[] { Ways, Normal, Centre, ActLeft, ActRight, Centring, Detent }, RefPrefix = "1V1", Uniform = false,
                Ports = PortsOf, PortLabel = PortName,
                Mounts = o =>
                {
                    var w = DesignOf(o).W / 2;
                    return new[] { new Port("left", -w, 0), new Port("right", w, 0) };
                },
                CompanionIds = new[] { "fluid.actuation", "fluid.line", "fluid.cylinder.double", "fluid.valve.throttle", "fluid.valve.check", "fluid.silencer" },
                Extra = Actions,
            };

            yield return new FluidType("fluid.actuation", T("Valve actuation", "Ventilbetätigung"), Cat,
                new[] { "actuation", "operator", "solenoid", "push button", "lever", "pedal", "roller", "spring", "pilot", "detent", "betätigung", "elektromagnet", "druckknopf", "hebel", "pedal", "rolle", "feder", "rastung" },
                Views.Symbol(5, 10, DrawActuation))
            {
                MediumDefault = "hydraulic", Params = new[] { ActKind }, Labeled = false, Uniform = false,
                Ports = o => o.Str("kind", "pushbutton") == "pilot" ? new[] { new Port("X", -2.5, 0) } : Array.Empty<Port>(),
                Mounts = Fixed(new Port("mount", 2.5, 0)),
                CompanionIds = new[] { TypeId, "fluid.line" },
                Extra = o => new[] { Cycle("fluid.actuation.next", T("Next actuation", "Nächste Betätigung"), ActKind) },
            };
        }

        // ---------------------------------------------------------------------------------------------------------
        // Layout
        // ---------------------------------------------------------------------------------------------------------

        public static int Positions(SketchObject o) => o.Str("ways", "4/3").EndsWith("/3", StringComparison.Ordinal) ? 3 : 2;

        public static int PortCount(SketchObject o)
        {
            var w = o.Str("ways", "4/3");
            return w.Length > 0 && char.IsDigit(w[0]) ? w[0] - '0' : 4;
        }

        private static double BoxWidth(SketchObject o) => PortCount(o) == 5 ? 15 : 10;

        /// <summary>Actuation elements of one side from the box outwards (detent and centring spring inside, operator outside).</summary>
        public static List<string> Elements(SketchObject o, bool left)
        {
            var list = new List<string>();
            var op = o.Str(left ? "actLeft" : "actRight", "solenoid");
            if (left && o.Flag("detent", false)) list.Add("detent");
            if (Positions(o) == 3 && o.Flag("centring", true) && op != "spring") list.Add("spring");
            if (op != "none") list.Add(op);
            return list;
        }

        public static Size2 DesignOf(SketchObject o)
            => new(Zone * Elements(o, true).Count + Positions(o) * BoxWidth(o) + Zone * Elements(o, false).Count, 2 * Half);

        /// <summary>Index of the box that shows the normal (rest) position and carries the ports.</summary>
        public static int RestBox(SketchObject o)
        {
            if (Positions(o) == 3) return 1;
            var l = o.Str("actLeft", "solenoid") == "spring";
            var r = o.Str("actRight", "solenoid") == "spring";
            return l && !r ? 0 : 1;
        }

        /// <summary>Left edge of the first box (design x).</summary>
        private static double BoxStart(SketchObject o) => -DesignOf(o).W / 2 + Zone * Elements(o, true).Count;

        /// <summary>Port offsets relative to a box centre (x) and edge (y = ±5).</summary>
        public static IReadOnlyList<Port> BoxPorts(SketchObject o) => PortCount(o) switch
        {
            2 => new[] { new Port("A", 0, -Half), new Port("P", 0, Half) },
            3 => new[] { new Port("A", -2.5, -Half), new Port("P", -2.5, Half), new Port("T", 2.5, Half) },
            5 => new[] { new Port("A", -2.5, -Half), new Port("B", 2.5, -Half), new Port("R", -5, Half), new Port("P", 0, Half), new Port("S", 5, Half) },
            _ => new[] { new Port("A", -2.5, -Half), new Port("B", 2.5, -Half), new Port("P", -2.5, Half), new Port("T", 2.5, Half) },
        };

        public static IReadOnlyList<Port> PortsOf(SketchObject o)
        {
            var bw = BoxWidth(o);
            var cx = BoxStart(o) + (RestBox(o) + 0.5) * bw;
            var list = BoxPorts(o).Select(p => new Port(p.Id, cx + p.X, p.Y)).ToList();
            var w = DesignOf(o).W / 2;
            if (o.Str("actLeft", "solenoid") == "pilot") list.Add(new Port("X", -w, 0));
            if (o.Str("actRight", "solenoid") == "pilot") list.Add(new Port("Y", w, 0));
            return list;
        }

        /// <summary>The return port of 3- and 4-port valves is named T (tank) for hydraulics and R (exhaust) for pneumatics; the id stays "T".</summary>
        public static string PortName(SketchObject o, string id)
            => id == "T" && Pneumatic(o) ? "R" : id;

        // ---------------------------------------------------------------------------------------------------------
        // Box contents
        // ---------------------------------------------------------------------------------------------------------

        private enum OpKind { Flow, Block, Join }

        private readonly record struct Op(OpKind Kind, string[] Ports);

        private static Op F(string a, string b) => new(OpKind.Flow, new[] { a, b });
        private static Op B(string a) => new(OpKind.Block, new[] { a });
        private static Op J(params string[] ports) => new(OpKind.Join, ports);

        /// <summary>Contents of each box from left to right.</summary>
        private static List<Op[]> Boxes(SketchObject o)
        {
            var ways = o.Str("ways", "4/3");
            var nc = o.Str("normal", "closed") != "open";
            Op[] rest, act;
            switch (ways)
            {
                case "2/2":
                    rest = nc ? new[] { B("P"), B("A") } : new[] { F("P", "A") };
                    act = nc ? new[] { F("P", "A") } : new[] { B("P"), B("A") };
                    break;
                case "3/2":
                    rest = nc ? new[] { F("A", "T"), B("P") } : new[] { F("P", "A"), B("T") };
                    act = nc ? new[] { F("P", "A"), B("T") } : new[] { F("A", "T"), B("P") };
                    break;
                case "4/2":
                    rest = new[] { F("P", "B"), F("A", "T") };
                    act = new[] { F("P", "A"), F("B", "T") };
                    break;
                case "5/2":
                    rest = new[] { F("P", "B"), F("A", "R"), B("S") };
                    act = new[] { F("P", "A"), F("B", "S"), B("R") };
                    break;
                case "5/3":
                    return new List<Op[]> { new[] { F("P", "A"), F("B", "S"), B("R") }, Centre5(o.Str("centre", "closed")), new[] { F("P", "B"), F("A", "R"), B("S") } };
                default: // 4/3
                    return new List<Op[]> { new[] { F("P", "A"), F("B", "T") }, Centre4(o.Str("centre", "closed")), new[] { F("P", "B"), F("A", "T") } };
            }
            return RestBox(o) == 0 ? new List<Op[]> { rest, act } : new List<Op[]> { act, rest };
        }

        private static Op[] Centre4(string centre) => centre switch
        {
            "tandem" => new[] { J("P", "T"), B("A"), B("B") },
            "float" => new[] { J("A", "B", "T"), B("P") },
            "open" => new[] { J("A", "B", "P", "T") },
            "pressure" => new[] { J("P", "A", "B"), B("T") },
            _ => new[] { B("A"), B("B"), B("P"), B("T") },
        };

        private static Op[] Centre5(string centre) => centre switch
        {
            "float" or "tandem" => new[] { F("A", "R"), F("B", "S"), B("P") },
            "pressure" => new[] { F("P", "A"), F("P", "B"), B("R"), B("S") },
            "open" => new[] { J("A", "B", "R", "P", "S") },
            _ => new[] { B("A"), B("B"), B("R"), B("P"), B("S") },
        };

        private static void Draw(Pen p, SketchObject o)
        {
            var n = Positions(o);
            var bw = BoxWidth(o);
            var x0 = BoxStart(o);
            var ports = BoxPorts(o).ToDictionary(q => q.Id);
            var boxes = Boxes(o);
            for (var i = 0; i < n; i++)
            {
                var bx = x0 + i * bw;
                p.Rect(bx, -Half, bx + bw, Half, null, "box");
                var cx = bx + bw / 2;
                foreach (var op in boxes[i]) DrawOp(p, op, cx, ports);
            }
            var hollow = Pneumatic(o);
            var e = x0;
            foreach (var el in Elements(o, true)) { Element(p, el, e, -1, hollow, n); e -= Zone; }
            e = x0 + n * bw;
            foreach (var el in Elements(o, false)) { Element(p, el, e, 1, hollow, n); e += Zone; }
        }

        private static void DrawOp(Pen p, Op op, double cx, IReadOnlyDictionary<string, Port> ports)
        {
            Vec2 At(string id) => ports.TryGetValue(id, out var q) ? new Vec2(cx + q.X, q.Y) : new Vec2(cx, 0);
            switch (op.Kind)
            {
                case OpKind.Flow:
                    {
                        var a = At(op.Ports[0]);
                        var b = At(op.Ports[1]);
                        p.Arrow(a.X, a.Y, b.X, b.Y);
                        break;
                    }
                case OpKind.Block:
                    {
                        var a = At(op.Ports[0]);
                        var y = a.Y * 0.5;
                        p.Line(a.X, a.Y, a.X, y, null, "block");
                        p.Line(a.X - 1.4, y, a.X + 1.4, y, null, "block");
                        break;
                    }
                default:
                    {
                        // Ports joined inside the box: lines from each port to a common bus line.
                        var pts = op.Ports.Select(At).ToList();
                        var top = pts.Where(q => q.Y < 0).ToList();
                        var bottom = pts.Where(q => q.Y > 0).ToList();
                        if (top.Count == 0 || bottom.Count == 0)
                        {
                            // Only one edge involved (tandem P to T): a U-shaped passage.
                            var y = pts[0].Y * 0.3;
                            foreach (var q in pts) p.Line(q.X, q.Y, q.X, y, null, "join");
                            p.Line(pts.Min(q => q.X), y, pts.Max(q => q.X), y, null, "join");
                        }
                        else
                        {
                            foreach (var q in pts) p.Line(q.X, q.Y, q.X, 0, null, "join");
                            var xs = pts.Select(q => q.X).Distinct().OrderBy(x => x).ToList();
                            var bar = xs.Count > 1;
                            if (bar) p.Line(xs[0], 0, xs[xs.Count - 1], 0, null, "join");
                            // Dot where three or more passages meet (T junctions), not at corners.
                            foreach (var x in xs)
                            {
                                var meet = pts.Count(q => Math.Abs(q.X - x) < 1e-9) + (!bar ? 0 : x == xs[0] || x == xs[xs.Count - 1] ? 1 : 2);
                                if (meet >= 3) p.Dot(x, 0, 0.45, "join-dot");
                            }
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// One actuation element attached at design x <paramref name="e"/> (box side or previous element), extending in
        /// direction <paramref name="dir"/> (−1 left, +1 right) by one zone width.
        /// </summary>
        public static void Element(Pen p, string kind, double e, int dir, bool hollow, int positions)
        {
            double X(double d) => e + dir * d;
            switch (kind)
            {
                case "spring":
                    p.SpringH(X(0), X(Zone), 0, 2.4, 3);
                    break;
                case "solenoid":
                    p.Rect(Math.Min(X(0), X(4)), -3, Math.Max(X(0), X(4)), 3, null, "solenoid");
                    p.Line(X(0), 3, X(4), -3, null, "solenoid");
                    break;
                case "manual":
                    p.Line(X(0), 0, X(4), 0, null, "operator");
                    p.Line(X(4), -3, X(4), 3, null, "operator");
                    break;
                case "pushbutton":
                    p.Line(X(0), 0, X(3), 0, null, "operator");
                    p.Line(X(3), -2, X(3), 2, null, "operator");
                    p.Arc(X(3), 0, 1.8, 2, dir < 0 ? 90 : 270, 180, null, "operator");
                    break;
                case "lever":
                    p.Line(X(0), 0, X(2.5), 0, null, "operator");
                    p.Line(X(1.5), 2.8, X(4), -3.2, null, "operator");
                    p.Round(X(4), -3.2, 0.7, S.Black, "operator");
                    break;
                case "pedal":
                    p.Line(X(0), 0, X(3.2), 0, null, "operator");
                    p.Line(X(2.4), 3, X(4.6), -3, null, "operator");
                    p.Line(X(2.4), 3, X(4.6), 3, null, "operator");
                    break;
                case "roller":
                    p.Line(X(0), 0, X(2.6), 0, null, "operator");
                    p.Round(X(3.7), 0, 1.1, null, "operator");
                    break;
                case "pilot":
                    p.Line(X(Zone), 0, X(2.2), 0, S.Pilot, "pilot");
                    p.Triangle(X(0), 0, X(Zone), 0, hollow, 2.2, 1.3, "pilot");
                    break;
                case "detent":
                    {
                        // Notched rod (one notch per position) and the latch engaging the first notch.
                        var step = 4.0 / positions;
                        p.Line(X(0), 2, X(4.6), 2, null, "detent");
                        var pts = new List<Vec2> { new(X(0), 0) };
                        for (var i = 0; i < positions; i++)
                        {
                            var a = 0.3 + i * step;
                            pts.Add(new Vec2(X(a), 0));
                            pts.Add(new Vec2(X(a + step / 2), 1.2));
                            pts.Add(new Vec2(X(a + step), 0));
                        }
                        pts.Add(new Vec2(X(4.6), 0));
                        p.PolyD(pts, null, false, "detent");
                        p.Line(X(0.3 + step / 2), 1.2, X(0.3 + step / 2), -4, null, "detent");
                        break;
                    }
            }
        }

        private static void DrawActuation(Pen p, SketchObject o)
        {
            var kind = o.Str("kind", "pushbutton");
            if (kind == "detent")
            {
                Element(p, "detent", 2.5, -1, false, 2);
                return;
            }
            Element(p, kind, 2.5, -1, Pneumatic(o), 2);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Context actions
        // ---------------------------------------------------------------------------------------------------------

        private static IEnumerable<ContextAction> Actions(SketchObject o)
        {
            yield return Cycle("fluid.valve.ways.next", T("Next valve type (2/2 … 5/3)", "Nächster Ventiltyp (2/2 … 5/3)"), Ways);
            var three = Positions(o) == 3;
            var centres = PortCount(o) == 5 ? Centres5 : Centres4;
            yield return Cycle("fluid.valve.centre.next", T("Next centre position", "Nächste Mittelstellung"), Centre, centres) with { IsEnabled = three };
            var twoWayOrThree = o.Str("ways", "4/3") is "2/2" or "3/2";
            yield return new ContextAction("fluid.valve.normal", T("Normally closed / open", "Ruhestellung gesperrt / offen"), IconIds.Toggle,
                x => x.WithParam("normal", x.Str("normal", "closed") == "open" ? "closed" : "open"))
            { IsActive = o.Str("normal", "closed") == "open", IsEnabled = twoWayOrThree, Group = "param" };
            yield return Cycle("fluid.valve.actLeft.next", T("Next actuation (left)", "Nächste Betätigung (links)"), ActLeft, Operators, "actuation", IconIds.Actuation);
            yield return Cycle("fluid.valve.actRight.next", T("Next actuation (right)", "Nächste Betätigung (rechts)"), ActRight, Operators, "actuation", IconIds.Actuation);
            yield return new ContextAction("fluid.valve.flip", T("Flip spring side (swap actuations)", "Federseite tauschen (Betätigungen tauschen)"), IconIds.Swap,
                x => x.WithParam("actLeft", x.Str("actRight", "solenoid")).WithParam("actRight", x.Str("actLeft", "solenoid")))
            { Group = "actuation" };
            yield return Toggle(o, "fluid.valve.detent", T("Detent", "Rastung"), "detent", false, IconIds.Toggle, "actuation");
            yield return Toggle(o, "fluid.valve.centring", T("Spring centred", "Federzentriert"), "centring", true, IconIds.Toggle, "actuation") with { IsEnabled = three };
        }
    }
}
