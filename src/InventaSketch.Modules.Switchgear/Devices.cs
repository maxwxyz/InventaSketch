using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>
    /// Modular installation devices on the DIN rail: circuit breakers, RCDs, RCBOs, surge protection, main switch, meter,
    /// contactors, relays, timer relays, push buttons, switches, indicator lamps, rail socket, bus devices and power
    /// supplies. Widths are whole modules (1 module = 17.5 mm) that follow the poles, channels or power.
    /// </summary>
    internal static class Devices
    {
        public const string Mcb = "sw.protection.mcb";
        public const string Rcd = "sw.protection.rcd";
        public const string Rcbo = "sw.protection.rcbo";
        public const string Spd = "sw.protection.spd";
        public const string MainSwitch = "sw.switch.main";
        public const string Meter = "sw.meter.energy";
        public const string Psu = "sw.supply.psu";
        public const string Contactor = "sw.switching.contactor";
        public const string Relay = "sw.switching.relay";
        public const string Impulse = "sw.switching.impulse";
        public const string Timer = "sw.switching.timer";
        public const string PushButton = "sw.control.pushbutton";
        public const string Switch = "sw.control.switch";
        public const string Lamp = "sw.control.lamp";
        public const string Socket = "sw.socket.dinrail";
        public const string BusActuator = "sw.bus.actuator";
        public const string BusCoupler = "sw.bus.coupler";
        public const string BusPower = "sw.bus.power";

        private static I18nText T(string en, string de) => new(en, de);
        private static string N(int i) => i.ToString(CultureInfo.InvariantCulture);
        private static Size2 Mod(double modules) => new(modules * Modular.Unit, Modular.DeviceHeight);

        // ------------------------------------------------------------------ shared parameters

        private static readonly ParamDef Characteristic = Pm.Choice("char", "Characteristic", "Auslösecharakteristik", "B",
            ("B", "B", "B"), ("C", "C", "C"), ("D", "D", "D"));
        private static readonly ParamDef McbRating = Pm.Values("rating", "Rated current (A)", "Bemessungsstrom (A)", "16",
            "6", "10", "13", "16", "20", "25", "32", "40", "50", "63");
        private static readonly ParamDef McbPoles = Pm.Choice("poles", "Poles", "Pole", "1",
            ("1", "1-pole", "1-polig"), ("1N", "1+N", "1+N"), ("2", "2-pole", "2-polig"), ("3", "3-pole", "3-polig"), ("3N", "3+N", "3+N"));
        private static readonly ParamDef RcdPoles = Pm.Choice("poles", "Poles", "Pole", "2",
            ("2", "2-pole (1+N)", "2-polig (1+N)"), ("4", "4-pole (3+N)", "4-polig (3+N)"));
        private static readonly ParamDef RcdRating = Pm.Values("rating", "Rated current (A)", "Bemessungsstrom (A)", "40", "25", "40", "63", "80", "100");
        private static readonly ParamDef Residual = Pm.Choice("idn", "Residual current", "Bemessungsfehlerstrom", "30",
            ("10", "10 mA", "10 mA"), ("30", "30 mA", "30 mA"), ("100", "100 mA", "100 mA"), ("300", "300 mA", "300 mA"), ("500", "500 mA", "500 mA"));
        private static readonly ParamDef RcdType = Pm.Choice("rcdType", "RCD type", "FI-Typ", "A",
            ("AC", "Type AC", "Typ AC"), ("A", "Type A", "Typ A"), ("F", "Type F", "Typ F"), ("B", "Type B", "Typ B"));
        private static readonly ParamDef RcboPoles = Pm.Choice("poles", "Poles", "Pole", "1N", ("1N", "1+N", "1+N"), ("3N", "3+N", "3+N"));
        private static readonly ParamDef RcboRating = Pm.Values("rating", "Rated current (A)", "Bemessungsstrom (A)", "16", "6", "10", "13", "16", "20", "25", "32", "40");
        private static readonly ParamDef RcboResidual = Pm.Choice("idn", "Residual current", "Bemessungsfehlerstrom", "30",
            ("10", "10 mA", "10 mA"), ("30", "30 mA", "30 mA"), ("100", "100 mA", "100 mA"), ("300", "300 mA", "300 mA"));
        private static readonly ParamDef SpdType = Pm.Choice("spdType", "SPD type", "SPD-Typ", "2",
            ("1", "Type 1", "Typ 1"), ("12", "Type 1+2", "Typ 1+2"), ("2", "Type 2", "Typ 2"), ("3", "Type 3", "Typ 3"));
        private static readonly ParamDef SpdPoles = Pm.Choice("poles", "Poles", "Pole", "3N",
            ("1", "1-pole", "1-polig"), ("1N", "1+N", "1+N"), ("3", "3-pole", "3-polig"), ("3N", "3+N", "3+N"));
        private static readonly ParamDef SwitchPoles = Pm.Choice("poles", "Poles", "Pole", "3",
            ("1", "1-pole", "1-polig"), ("2", "2-pole", "2-polig"), ("3", "3-pole", "3-polig"), ("4", "4-pole", "4-polig"));
        private static readonly ParamDef SwitchRating = Pm.Values("rating", "Rated current (A)", "Bemessungsstrom (A)", "63", "32", "40", "63", "80", "100", "125");
        private static readonly ParamDef Phases = Pm.Choice("phases", "Phases", "Phasen", "1", ("1", "Single-phase", "Einphasig"), ("3", "Three-phase", "Dreiphasig"));
        private static readonly ParamDef Coil = Pm.Choice("coil", "Coil voltage", "Spulenspannung", "230ac",
            ("230ac", "230 V AC", "230 V AC"), ("24ac", "24 V AC", "24 V AC"), ("24dc", "24 V DC", "24 V DC"), ("12dc", "12 V DC", "12 V DC"));
        private static readonly ParamDef RelayCoil = Coil with { Default = "24dc" };
        private static readonly ParamDef ContactorContacts = Pm.Choice("contacts", "Contacts", "Kontakte", "2",
            ("2", "2 NO", "2 Schließer"), ("4", "4 NO", "4 Schließer"), ("22", "2 NO + 2 NC", "2 Schließer + 2 Öffner"));
        private static readonly ParamDef RelayContacts = Pm.Choice("contacts", "Contacts", "Kontakte", "1co",
            ("1co", "1 changeover", "1 Wechsler"), ("2co", "2 changeovers", "2 Wechsler"));
        private static readonly ParamDef ImpulseContacts = Pm.Choice("contacts", "Contacts", "Kontakte", "1",
            ("1", "1 NO", "1 Schließer"), ("2", "2 NO", "2 Schließer"));
        private static readonly ParamDef TimerFunction = Pm.Choice("function", "Function", "Funktion", "ondelay",
            ("ondelay", "On-delay", "Ansprechverzögert"), ("offdelay", "Off-delay", "Rückfallverzögert"),
            ("staircase", "Staircase timer", "Treppenlicht"), ("flasher", "Flasher", "Blinker"));
        private static readonly ParamDef TimeText = Pm.Text("time", "Time (e.g. 3 min)", "Zeit (z. B. 3 min)");
        private static readonly ParamDef ButtonContact = Pm.Choice("contact", "Contact", "Kontakt", "no", ("no", "Normally open", "Schließer"), ("nc", "Normally closed", "Öffner"));
        private static readonly ParamDef ButtonColour = Pm.Choice("colour", "Colour", "Farbe", "black",
            ("black", "Black", "Schwarz"), ("green", "Green", "Grün"), ("red", "Red", "Rot"), ("yellow", "Yellow", "Gelb"), ("blue", "Blue", "Blau"));
        private static readonly ParamDef LampColour = Pm.Choice("colour", "Colour", "Farbe", "green",
            ("green", "Green", "Grün"), ("red", "Red", "Rot"), ("yellow", "Yellow", "Gelb"), ("white", "White", "Weiß"), ("blue", "Blue", "Blau"));
        private static readonly ParamDef SwitchKind = Pm.Choice("kind", "Kind", "Art", "onoff", ("onoff", "On/off (0-I)", "Ein/Aus (0-I)"), ("selector", "Selector (I-0-II)", "Wahlschalter (I-0-II)"));
        private static readonly ParamDef Voltage = Pm.Values("voltage", "Output voltage (V)", "Ausgangsspannung (V)", "24", "5", "12", "24", "48");
        private static readonly ParamDef Power = Pm.Values("power", "Power (W)", "Leistung (W)", "60", "15", "30", "60", "120");
        private static readonly ParamDef Bus = Pm.Choice("bus", "Bus", "Bus", "knx", ("knx", "KNX", "KNX"), ("generic", "Generic bus", "Allgemeiner Bus"));
        private static readonly ParamDef BusFunction = Pm.Choice("function", "Function", "Funktion", "switch",
            ("switch", "Switching", "Schalten"), ("dimmer", "Dimming", "Dimmen"), ("blind", "Blinds", "Jalousie"), ("input", "Binary input", "Binäreingang"));
        private static readonly ParamDef Channels = Pm.Values("channels", "Channels", "Kanäle", "4", "1", "2", "4", "8", "12");
        private static readonly ParamDef BusCurrent = Pm.Choice("current", "Output current", "Ausgangsstrom", "640", ("160", "160 mA", "160 mA"), ("320", "320 mA", "320 mA"), ("640", "640 mA", "640 mA"));

        public static readonly string[] BoardCompanions = { Mcb, Rcd, Rcbo, Rails.RailId, Labels.StripId };

        // ------------------------------------------------------------------ pole helpers

        /// <summary>Phase count and neutral pole of a "poles" value ("1", "1N", "2", "3", "3N", "4").</summary>
        public static (int Phases, bool Neutral) Poles(string value) => value switch
        {
            "1N" => (1, true),
            "2" => (2, false),
            "3" => (3, false),
            "3N" => (3, true),
            "4" => (4, false),
            _ => (1, false),
        };

        private static IReadOnlyList<Column> PoleColumns(int phases, bool neutral, ColumnKind kind, ColumnKind neutralKind)
        {
            var list = new List<Column>();
            for (var i = 0; i < phases; i++) list.Add(new Column(N(2 * i + 1), N(2 * i + 2), kind));
            if (neutral) list.Add(new Column("N1", "N2", neutralKind));
            return list;
        }

        private static string CoilText(SketchObject o, string fallback)
            => (Coil.Options ?? Array.Empty<ParamOption>()).FirstOrDefault(x => x.Value == o.Str("coil", fallback))?.Label.En ?? "";

        private static string Fill(string colour) => colour switch
        {
            "green" => "#43a047",
            "red" => "#e53935",
            "yellow" => "#fdd835",
            "blue" => "#1e88e5",
            "white" => "#fafafa",
            _ => "#263238",
        };

        private static string BusName(SketchObject o) => o.Str("bus", "knx") == "knx" ? "KNX" : "BUS";

        // ------------------------------------------------------------------ specs

        public static IEnumerable<DeviceSpec> Specs()
        {
            yield return new DeviceSpec(Mcb, T("Miniature circuit breaker (DIN rail)", "Leitungsschutzschalter (Hutschiene)"), SwCategories.Protection, DeviceRole.Protection, "-F")
            {
                Keywords = new[] { "mcb", "breaker", "ls", "leitungsschutzschalter", "sicherungsautomat", "b16", "c16" },
                Params = new[] { Characteristic, McbRating, McbPoles },
                Natural = o => { var (ph, n) = Poles(o.Str("poles", "1")); return Mod(ph + (n ? 1 : 0)); },
                Columns = o => { var (ph, n) = Poles(o.Str("poles", "1")); return PoleColumns(ph, n, ColumnKind.Breaker, ColumnKind.Disconnector); },
                Rating = o => o.Str("char", "B") + o.Str("rating", "16"),
                Lever = true, Linked = true,
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    var on = o.Flag("on", true);
                    var (ph, _) = Poles(o.Str("poles", "1"));
                    var slot = p.W / xs.Count;
                    if (xs.Count > 1) p.Rect(xs[0] - 2.6, on ? -8.2 : 2.3, xs[xs.Count - 1] - xs[0] + 5.2, 1.8, Sx.ThinFill(Sx.Lever), 0.4, SwTags.Lever);
                    for (var i = 0; i < xs.Count; i++)
                    {
                        Fronts.Lever(p, xs[i], 5.4, on);
                        Fronts.Window(p, xs[i], on);
                        p.FitText(xs[i], 17.6, i < ph ? o.Str("char", "B") + o.Str("rating", "16") : "N", 4.0, slot - 2, bold: true, tag: SwTags.Text);
                    }
                },
            };

            yield return new DeviceSpec(Rcd, T("Residual current device (DIN rail)", "Fehlerstromschutzschalter (Hutschiene)"), SwCategories.Protection, DeviceRole.Rcd, "-F")
            {
                Keywords = new[] { "rcd", "rccb", "fi", "fehlerstrom", "30 ma", "residual" },
                Params = new[] { RcdPoles, RcdRating, Residual, RcdType },
                Natural = o => Mod(o.Str("poles", "2") == "4" ? 4 : 2),
                Columns = o => o.Str("poles", "2") == "4" ? PoleColumns(3, true, ColumnKind.NormallyOpen, ColumnKind.NormallyOpen) : PoleColumns(1, true, ColumnKind.NormallyOpen, ColumnKind.NormallyOpen),
                Rating = o => o.Str("rating", "40") + " A " + o.Str("idn", "30") + " mA",
                Lever = true, Linked = true, Residual = true,
                Companions = BoardCompanions,
                Front = (p, o, xs) => ResidualFront(p, o, o.Str("rating", "40") + " A", o.Str("idn", "30")),
            };

            yield return new DeviceSpec(Rcbo, T("RCBO (RCD with circuit breaker)", "FI/LS-Schalter (Hutschiene)"), SwCategories.Protection, DeviceRole.Rcbo, "-F")
            {
                Keywords = new[] { "rcbo", "fi/ls", "fi-ls", "kombischalter", "residual", "breaker" },
                Params = new[] { RcboPoles, Characteristic, RcboRating, RcboResidual, RcdType },
                Natural = o => Mod(o.Str("poles", "1N") == "3N" ? 4 : 2),
                Columns = o => o.Str("poles", "1N") == "3N" ? PoleColumns(3, true, ColumnKind.Breaker, ColumnKind.Disconnector) : PoleColumns(1, true, ColumnKind.Breaker, ColumnKind.Disconnector),
                Rating = o => o.Str("char", "B") + o.Str("rating", "16") + " " + o.Str("idn", "30") + " mA",
                Lever = true, Linked = true, Residual = true,
                Companions = BoardCompanions,
                Front = (p, o, xs) => ResidualFront(p, o, o.Str("char", "B") + o.Str("rating", "16"), o.Str("idn", "30")),
            };

            yield return new DeviceSpec(Spd, T("Surge protective device (DIN rail)", "Überspannungsschutz (Hutschiene)"), SwCategories.Protection, DeviceRole.Spd, "-F")
            {
                Keywords = new[] { "spd", "surge", "überspannung", "blitzstrom", "type 2", "typ 2" },
                Params = new[] { SpdType, SpdPoles },
                Natural = o => { var (ph, n) = Poles(o.Str("poles", "3N")); return Mod(ph + (n ? 1 : 0)); },
                Columns = o =>
                {
                    var (ph, n) = Poles(o.Str("poles", "3N"));
                    var list = new List<Column>();
                    for (var i = 0; i < ph; i++) list.Add(new Column("L" + N(i + 1), null, ColumnKind.Spd));
                    if (n) list.Add(new Column("N", null, ColumnKind.Spd));
                    list.Add(new Column(null, "PE", ColumnKind.Terminal) { FrontU = 0 });
                    return list;
                },
                Rating = o => "T" + (o.Str("spdType", "2") == "12" ? "1+2" : o.Str("spdType", "2")),
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    var slot = p.W / xs.Count;
                    var label = "T" + (o.Str("spdType", "2") == "12" ? "1+2" : o.Str("spdType", "2"));
                    foreach (var x in xs)
                    {
                        p.Rect(x - slot / 2 + 1, -14.5, slot - 2, 36.5, Sx.ThinFill("#f5f5f5"), 0.8, SwTags.Front);
                        p.CRect(x, -9.5, Math.Min(6, slot - 4), 3.6, Sx.Solid(Sx.Off), 0.4, SwTags.Front);
                        Fronts.Lightning(p, x, 3);
                        p.FitText(x, 15.5, label, 3.0, slot - 3, bold: true, tag: SwTags.Text);
                    }
                },
            };

            yield return new DeviceSpec(MainSwitch, T("Main switch (switch-disconnector)", "Hauptschalter (Lasttrennschalter)"), SwCategories.Protection, DeviceRole.MainSwitch, "-Q")
            {
                Keywords = new[] { "main switch", "isolator", "disconnector", "hauptschalter", "trennschalter", "lasttrennschalter" },
                Params = new[] { SwitchPoles, SwitchRating },
                Natural = o => Mod(Poles(o.Str("poles", "3")).Phases),
                Columns = o => PoleColumns(Poles(o.Str("poles", "3")).Phases, false, ColumnKind.Disconnector, ColumnKind.Disconnector),
                Rating = o => o.Str("rating", "63") + " A",
                Lever = true, Linked = true,
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    var on = o.Flag("on", true);
                    Fronts.Lever(p, 0, Math.Max(5, p.W - 7), on, Sx.LeverRed);
                    p.Text(0, -14, "I", 2.2, bold: true);
                    p.Text(0, 10.2, "0", 2.2, bold: true);
                    p.FitText(0, 17.4, o.Str("rating", "63") + " A", 3.8, p.W - 3, bold: true, tag: SwTags.Text);
                },
            };

            yield return new DeviceSpec(Meter, T("Energy meter (DIN rail)", "Energiezähler (Hutschiene)"), SwCategories.Supply, DeviceRole.Meter, "-P")
            {
                Keywords = new[] { "meter", "kwh", "zähler", "stromzähler", "wechselstromzähler", "drehstromzähler" },
                Params = new[] { Phases },
                Natural = o => Mod(o.Str("phases", "1") == "3" ? 4 : 2),
                Columns = o => o.Str("phases", "1") == "3" ? PoleColumns(3, true, ColumnKind.Box, ColumnKind.Box) : PoleColumns(1, true, ColumnKind.Box, ColumnKind.Box),
                Rating = o => o.Str("phases", "1") == "3" ? "3~" : "1~",
                SymbolText = _ => "kWh",
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    p.Rect(-p.W / 2 + 2.5, -13.8, p.W - 5, 9.4, Sx.ThinFill(Sx.Display), 0.5, SwTags.Front);
                    p.FitText(0, -9.1, "000123.4", 4.2, p.W - 7, colour: Sx.DisplayFrame, tag: SwTags.Front);
                    p.Text(p.W / 2 - 3, -1.6, "kWh", 2.2, anchor: TextAnchor.End);
                    Fronts.Led(p, -p.W / 2 + 4.5, 4.6, Sx.Led);
                    p.Text(-p.W / 2 + 6.4, 4.6, "imp", 1.8, anchor: TextAnchor.Start);
                    p.Text(0, 16.5, o.Str("phases", "1") == "3" ? "3~" : "1~", 3.4, bold: true);
                },
            };

            yield return new DeviceSpec(Psu, T("DIN-rail power supply", "Hutschienennetzteil"), SwCategories.Supply, DeviceRole.Supply, "-T")
            {
                Keywords = new[] { "power supply", "psu", "netzteil", "stromversorgung", "24 v", "smps" },
                Params = new[] { Voltage, Power },
                Natural = o => Mod(o.Str("power", "60") switch { "15" => 1, "30" => 2, "120" => 4, _ => 3 }),
                Columns = _ => new[] { new Column("L1", "V+", ColumnKind.Box), new Column("N1", "V-", ColumnKind.Box) },
                Rating = o => o.Str("voltage", "24") + " V DC " + o.Str("power", "60") + " W",
                SymbolText = _ => "~ / =",
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    for (var x = -p.W / 2 + 3; x < p.W / 2 - 2; x += 2.4) p.Line(x, -13.5, x + 1.6, -9.5, Sx.Thin, SwTags.Front);
                    p.FitText(0, -4, o.Str("voltage", "24") + " V DC", 3.6, p.W - 3, bold: true, tag: SwTags.Text);
                    p.FitText(0, 2.5, o.Str("power", "60") + " W", 2.8, p.W - 3, tag: SwTags.Text);
                    Fronts.Led(p, -p.W / 2 + 3.4, 12.5, Sx.LedGreen);
                    p.Text(-p.W / 2 + 5, 12.5, "DC OK", 1.6, anchor: TextAnchor.Start);
                    p.Circle(p.W / 2 - 3.6, 12.5, 1.6, Sx.ThinFill(Sx.Screw), SwTags.Front);
                    p.Line(p.W / 2 - 4.6, 12.5, p.W / 2 - 2.6, 12.5, Sx.Thin, SwTags.Front);
                },
            };

            yield return new DeviceSpec(Contactor, T("Installation contactor", "Installationsschütz"), SwCategories.Switching, DeviceRole.Switching, "-Q")
            {
                Keywords = new[] { "contactor", "schütz", "installationsschütz", "relay", "a1 a2" },
                Params = new[] { ContactorContacts, Coil },
                Natural = o => Mod(o.Str("contacts", "2") == "2" ? 1 : 2),
                Columns = o =>
                {
                    var c = o.Str("contacts", "2");
                    var list = new List<Column> { new("A1", "A2", ColumnKind.Coil) };
                    var no = c == "2" || c == "22" ? 2 : 4;
                    for (var i = 0; i < no; i++) list.Add(new Column(N(2 * i + 1), N(2 * i + 2), ColumnKind.Contactor));
                    if (c == "22") { list.Add(new Column("R1", "R2", ColumnKind.NormallyClosed)); list.Add(new Column("R3", "R4", ColumnKind.NormallyClosed)); }
                    return list;
                },
                Rating = o => CoilText(o, "230ac"),
                Linked = true,
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    p.CRect(0, -7, Math.Min(p.W - 6, 10), 6.4, Sx.ThinFill("#263238"), 0.6, SwTags.Front);
                    p.CRect(0, -7, 4, 1.4, Sx.Solid("#ffffff"), 0.2, SwTags.Front);
                    p.FitText(0, 6, CoilText(o, "230ac"), 2.8, p.W - 3, tag: SwTags.Text);
                    p.FitText(0, 15.5, "A1-A2", 2.2, p.W - 3, tag: SwTags.Text);
                },
            };

            yield return new DeviceSpec(Relay, T("Coupling relay (DIN rail)", "Koppelrelais (Hutschiene)"), SwCategories.Switching, DeviceRole.Switching, "-K")
            {
                Keywords = new[] { "relay", "relais", "koppelrelais", "interface relay", "changeover", "wechsler" },
                Params = new[] { RelayContacts, RelayCoil },
                Natural = o => Mod(o.Str("contacts", "1co") == "2co" ? 2 : 1),
                Columns = o =>
                {
                    var list = new List<Column> { new("A1", "A2", ColumnKind.Coil), new("11", "14", ColumnKind.Changeover) { Bottom2 = "12" } };
                    if (o.Str("contacts", "1co") == "2co") list.Add(new Column("21", "24", ColumnKind.Changeover) { Bottom2 = "22" });
                    return list;
                },
                Rating = o => CoilText(o, "24dc"),
                Linked = true,
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    Fronts.Led(p, 0, -11, Sx.LedGreen, 1.1);
                    p.CRect(0, -2, Math.Min(p.W - 6, 9), 8, Sx.ThinFill("#eceff1"), 0.6, SwTags.Front);
                    p.Line(-2, -2, 2, -4.5, Sx.Thin, SwTags.Front);
                    p.FitText(0, 10, CoilText(o, "24dc"), 2.6, p.W - 3, tag: SwTags.Text);
                },
            };

            yield return new DeviceSpec(Impulse, T("Impulse relay (DIN rail)", "Stromstoßschalter (Hutschiene)"), SwCategories.Switching, DeviceRole.Switching, "-K")
            {
                Keywords = new[] { "impulse relay", "latching relay", "stromstoßschalter", "eltako", "toggle relay" },
                Params = new[] { ImpulseContacts, Coil },
                Natural = _ => Mod(1),
                Columns = o =>
                {
                    var list = new List<Column> { new("A1", "A2", ColumnKind.Coil), new("1", "2", ColumnKind.NormallyOpen) };
                    if (o.Str("contacts", "1") == "2") list.Add(new Column("3", "4", ColumnKind.NormallyOpen));
                    return list;
                },
                Rating = o => CoilText(o, "230ac"),
                Linked = true,
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    p.CRect(0, -4, 6, 14, Sx.ThinFill(Sx.Lever), 0.8, SwTags.Lever);
                    p.Text(0, -8.5, "I", 2.0, bold: true, colour: "#ffffff", tag: SwTags.Lever);
                    p.Text(0, 0.5, "0", 2.0, bold: true, colour: "#ffffff", tag: SwTags.Lever);
                    p.FitText(0, 12, CoilText(o, "230ac"), 2.6, p.W - 3, tag: SwTags.Text);
                },
            };

            yield return new DeviceSpec(Timer, T("Timer relay (DIN rail)", "Zeitrelais (Hutschiene)"), SwCategories.Switching, DeviceRole.Switching, "-K")
            {
                Keywords = new[] { "timer", "time relay", "zeitrelais", "treppenlicht", "staircase", "verzögerung", "delay" },
                Params = new[] { TimerFunction, TimeText, Coil },
                Natural = _ => Mod(1),
                Columns = _ => new[] { new Column("A1", "A2", ColumnKind.Coil), new Column("15", "18", ColumnKind.Changeover) { Bottom2 = "16" } },
                Rating = o => o.Str("time", "").Trim(),
                Linked = true,
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    var r = Math.Min(5.2, p.W / 2 - 2);
                    p.Circle(0, -7, r, Sx.ThinFill("#ffffff"), SwTags.Front);
                    for (var i = 0; i < 7; i++)
                    {
                        var a = Geo.DegToRad(135 + i * 45);
                        p.Line(Math.Cos(a) * r * 0.78, -7 + Math.Sin(a) * r * 0.78, Math.Cos(a) * r, -7 + Math.Sin(a) * r, Sx.Thin, SwTags.Front);
                    }
                    p.Line(0, -7, r * 0.5, -7 - r * 0.5, Style.Visible, SwTags.Front);
                    Fronts.Led(p, p.W / 2 - 2.2, -13.4, Sx.LedGreen, 0.8);
                    Fronts.TimerDiagram(p, 0, 5, o.Str("function", "ondelay"), Math.Min(12, p.W - 4));
                    p.FitText(0, 15.5, o.Str("time", ""), 2.6, p.W - 3, tag: SwTags.Text);
                },
            };

            yield return new DeviceSpec(PushButton, T("Push button for DIN rail", "Taster für Hutschiene"), SwCategories.Switching, DeviceRole.Control, "-S")
            {
                Keywords = new[] { "push button", "taster", "drucktaster", "reiheneinbau" },
                Params = new[] { ButtonContact, ButtonColour },
                Natural = _ => Mod(1),
                Columns = o => o.Str("contact", "no") == "nc" ? new[] { new Column("11", "12", ColumnKind.PushClosed) } : new[] { new Column("13", "14", ColumnKind.PushOpen) },
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    var r = Math.Min(6.2, p.W / 2 - 1.6);
                    p.Circle(0, -1, r, Sx.ThinFill("#9e9e9e"), SwTags.Front);
                    p.Circle(0, -1, r * 0.76, Sx.ThinFill(Fill(o.Str("colour", "black"))), SwTags.Lever);
                },
            };

            yield return new DeviceSpec(Switch, T("Switch for DIN rail", "Schalter für Hutschiene"), SwCategories.Switching, DeviceRole.Control, "-S")
            {
                Keywords = new[] { "switch", "schalter", "wahlschalter", "selector", "reiheneinbau" },
                Params = new[] { SwitchKind },
                Natural = _ => Mod(1),
                Columns = o => o.Str("kind", "onoff") == "selector"
                    ? new[] { new Column("1", "2", ColumnKind.NormallyOpen), new Column("3", "4", ColumnKind.NormallyOpen) }
                    : new[] { new Column("1", "2", ColumnKind.NormallyOpen) },
                Lever = true,
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    if (o.Str("kind", "onoff") == "selector")
                    {
                        var r = Math.Min(5.6, p.W / 2 - 2);
                        p.Circle(0, -1, r, Sx.ThinFill("#263238"), SwTags.Lever);
                        var a = Geo.DegToRad(o.Flag("on", true) ? -45 : -90);
                        p.Line(-Math.Cos(a) * r * 0.8, -1 - Math.Sin(a) * r * 0.8, Math.Cos(a) * r * 0.8, -1 + Math.Sin(a) * r * 0.8,
                            new Style { Role = LineRole.Visible, Stroke = "#ffffff", StrokeWidth = 0.6 }, SwTags.Lever);
                        p.Text(-p.W / 2 + 2.2, -9.5, "I", 1.8, bold: true);
                        p.Text(0, -9.8, "0", 1.8, bold: true);
                        p.Text(p.W / 2 - 2.6, -9.5, "II", 1.8, bold: true);
                    }
                    else
                    {
                        Fronts.Lever(p, 0, 5.4, o.Flag("on", true));
                        p.Text(-p.W / 2 + 2.4, -10, "I", 1.8, bold: true);
                        p.Text(-p.W / 2 + 2.4, 6.5, "0", 1.8, bold: true);
                    }
                },
            };

            yield return new DeviceSpec(Lamp, T("Indicator lamp for DIN rail", "Leuchtmelder für Hutschiene"), SwCategories.Switching, DeviceRole.Control, "-P")
            {
                Keywords = new[] { "indicator", "pilot lamp", "leuchtmelder", "meldeleuchte", "kontrollleuchte" },
                Params = new[] { LampColour },
                Natural = _ => Mod(1),
                Columns = _ => new[] { new Column("X1", "X2", ColumnKind.Box) },
                SymbolText = _ => "lamp",
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    var r = Math.Min(6.2, p.W / 2 - 1.6);
                    p.Circle(0, -1, r, Sx.ThinFill("#9e9e9e"), SwTags.Front);
                    p.Circle(0, -1, r * 0.78, Sx.ThinFill(Fill(o.Str("colour", "green"))) with { Opacity = 0.85 }, SwTags.Lever);
                    p.Circle(-r * 0.3, -1 - r * 0.3, r * 0.18, Sx.Solid("#ffffff") with { Opacity = 0.8 }, SwTags.Lever);
                },
            };

            yield return new DeviceSpec(Socket, T("Socket outlet for DIN rail", "Steckdose für Hutschiene"), SwCategories.Switching, DeviceRole.Socket, "-X")
            {
                Keywords = new[] { "socket", "steckdose", "schuko", "service socket", "reiheneinbausteckdose" },
                Natural = _ => Mod(3),
                Columns = _ => new[] { new Column("L1", null, ColumnKind.Box), new Column("N1", null, ColumnKind.Box), new Column("PE", null, ColumnKind.Box) },
                SymbolText = _ => "socket",
                Companions = BoardCompanions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""), -p.W / 2 + 9, 14);
                    p.Circle(0, 1.5, 13.4, Sx.ThinFill("#e0e0e0"), SwTags.Front);
                    p.Circle(-4.75, 1.5, 1.9, Sx.ThinFill("#37474f"), SwTags.Front);
                    p.Circle(4.75, 1.5, 1.9, Sx.ThinFill("#37474f"), SwTags.Front);
                    p.CRect(0, 1.5 - 11.6, 6, 1.8, Sx.ThinFill(Sx.Screw), 0.3, SwTags.Front);
                    p.CRect(0, 1.5 + 11.6, 6, 1.8, Sx.ThinFill(Sx.Screw), 0.3, SwTags.Front);
                },
            };

            yield return new DeviceSpec(BusActuator, T("Bus actuator (DIN rail)", "Busaktor (Hutschiene)"), SwCategories.Supply, DeviceRole.Bus, "-K")
            {
                Keywords = new[] { "knx", "eib", "actuator", "aktor", "schaltaktor", "dimmaktor", "jalousieaktor", "binäreingang", "binary input", "bus" },
                Params = new[] { Bus, BusFunction, Channels },
                Natural = o => Mod(o.Str("channels", "4") switch { "8" => 8, "12" => 12, "1" => 2, "2" => 2, _ => 4 }),
                Columns = o =>
                {
                    var n = Math.Max(1, Math.Min(12, (int)Num.ParseOr(o.Str("channels", "4"), 4)));
                    var list = new List<Column> { new("bus", null, ColumnKind.Box) { Medium = PortMedia.Data } };
                    for (var i = 0; i < n; i++)
                    {
                        var ch = ((char)('A' + i)).ToString();
                        list.Add(new Column(ch + "1", ch + "2", ColumnKind.Box));
                    }
                    return list;
                },
                Rating = o => o.Str("channels", "4") + "×",
                SymbolText = BusName,
                Companions = new[] { BusPower, BusCoupler, Rails.RailId },
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    p.Circle(-p.W / 2 + 4, -11.5, 1.5, Sx.ThinFill("#263238"), SwTags.Front);
                    Fronts.Led(p, -p.W / 2 + 4, -7.6, Sx.Led, 0.75);
                    p.Text(p.W / 2 - 2, -11.5, BusName(o), 2.8, bold: true, anchor: TextAnchor.End);
                    var blind = o.Str("function", "switch") == "blind";
                    for (var i = 1; i < xs.Count; i++)
                    {
                        var x = xs[i];
                        var slot = p.W / xs.Count;
                        Fronts.Led(p, x, -1.5, o.Str("function", "switch") == "input" ? Sx.LedGreen : "#ffb300", 0.75);
                        p.CRect(x, 5, Math.Min(slot - 1.6, 6), 4.4, Sx.ThinFill("#cfd8dc"), 0.5, SwTags.Lever);
                        if (blind)
                        {
                            p.Poly(Sx.Solid("#37474f"), true, SwTags.Lever, (x - 1.2, 4.6), (x + 1.2, 4.6), (x, 3.4));
                            p.Poly(Sx.Solid("#37474f"), true, SwTags.Lever, (x - 1.2, 5.4), (x + 1.2, 5.4), (x, 6.6));
                        }
                        p.Text(x, 12.5, ((char)('A' + i - 1)).ToString(), 2.2, bold: true);
                    }
                },
            };

            yield return new DeviceSpec(BusCoupler, T("Bus coupler (DIN rail)", "Buskoppler (Hutschiene)"), SwCategories.Supply, DeviceRole.Bus, "-K")
            {
                Keywords = new[] { "knx", "line coupler", "linienkoppler", "bereichskoppler", "coupler", "koppler", "bus" },
                Params = new[] { Bus },
                Natural = _ => Mod(2),
                Columns = _ => new[] { new Column("bus1", null, ColumnKind.Box) { Medium = PortMedia.Data }, new Column(null, "bus2", ColumnKind.Box) { Medium = PortMedia.Data } },
                SymbolText = BusName,
                Companions = new[] { BusPower, BusActuator, Rails.RailId },
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    p.Text(0, -11, BusName(o), 3.2, bold: true);
                    p.Circle(-p.W / 2 + 4, -4, 1.5, Sx.ThinFill("#263238"), SwTags.Front);
                    Fronts.Led(p, -p.W / 2 + 4, 0, Sx.Led, 0.75);
                    Fronts.Led(p, 4, -4, Sx.LedGreen, 0.8);
                    Fronts.Led(p, 4, 4, Sx.LedGreen, 0.8);
                    p.Line(8, -4.5, 8, 4.5, Sx.Thin with { ArrowStart = ArrowHead.Triangle, ArrowEnd = ArrowHead.Triangle }, SwTags.Front);
                },
            };

            yield return new DeviceSpec(BusPower, T("Bus power supply (DIN rail)", "Busspannungsversorgung (Hutschiene)"), SwCategories.Supply, DeviceRole.Bus, "-T")
            {
                Keywords = new[] { "knx", "bus power", "spannungsversorgung", "drossel", "640 ma", "bus" },
                Params = new[] { Bus, BusCurrent },
                Natural = _ => Mod(4),
                Columns = _ => new[]
                {
                    new Column("L1", null, ColumnKind.Box), new Column("N1", null, ColumnKind.Box), new Column(null, "bus", ColumnKind.Box) { Medium = PortMedia.Data },
                },
                Rating = o => o.Str("current", "640") + " mA",
                SymbolText = BusName,
                Companions = new[] { BusActuator, BusCoupler, Rails.RailId },
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    p.Text(0, -10.5, BusName(o), 4.0, bold: true);
                    p.FitText(0, -3, o.Str("current", "640") + " mA", 3.0, p.W - 4, tag: SwTags.Text);
                    Fronts.Led(p, -12, 6, Sx.LedGreen);
                    Fronts.Led(p, -6, 6, Sx.Led);
                    p.CRect(10, 6, 7, 3.6, Sx.ThinFill("#cfd8dc"), 0.5, SwTags.Lever);
                    p.Text(10, 13, "Reset", 1.8);
                },
            };
        }

        /// <summary>Front of an RCD or RCBO: lever at the left, test button, type pictogram, residual current and rating.</summary>
        private static void ResidualFront(Pen p, SketchObject o, string rating, string idn)
        {
            var w = p.W;
            Fronts.Body(p, w);
            Fronts.LabelField(p, w, o.Str("ref", ""));
            var on = o.Flag("on", true);
            var lx = -w / 4;
            Fronts.Lever(p, lx, w >= 60 ? 12 : 7, on);
            Fronts.Window(p, lx, on);
            Fronts.TestButton(p, w / 4, -10.5);
            Fronts.RcdType(p, w / 4, -2.2, o.Str("rcdType", "A"));
            p.FitText(w / 4, 5.6, idn + " mA", 3.4, w / 2 - 2, bold: true, tag: SwTags.Text);
            p.FitText(0, 18, rating, 3.6, w - 4, bold: true, tag: SwTags.Text);
        }
    }
}
