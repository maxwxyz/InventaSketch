using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>
    /// Electrical installation and switchgear: distribution boards and control cabinets on TS35 DIN rails. Modular devices
    /// (circuit breakers, RCDs, RCBOs, surge protection, main switch, meter, contactors, relays, timer relays, bus devices,
    /// power supplies, push buttons, switches, lamps, rail socket) in whole modules of 17.5 mm with front views that look
    /// like the real devices and circuit symbol views; industrial devices (motor protection circuit breaker, overload relay,
    /// NH fuse switch-disconnector, frequency inverter, PLC modules, safety relay); terminal blocks with bridges; rails,
    /// comb busbars, blanking plates, ducts, end clamps and labels; wires and cables; installation devices in the rooms.
    /// Designations (IEC 81346 ideas) and ratings are optional parameters. Element values (<see cref="SwValues"/>): rated
    /// and operating current, power and phase per circuit and load, totals per phase on a rail, with plausibility checks.
    /// Exports: house installation list, circuit plan, board layout with labelling strip, terminal plan and cable list
    /// (CSV and Markdown). Draws everything itself; no dependency on other modules.
    /// </summary>
    public sealed class SwitchgearModule : IModule, IExportProvider
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() =>
            Devices.Specs().Concat(Industrial.Specs()).Concat(Terminals.Specs()).Concat(Accessories.Specs()).Concat(Industrial.AccessorySpecs()).Select(s => (ObjectType)new DeviceType(s))
                .Concat(new ObjectType[] { new RailType(), new DuctType(), new StripType(), new PlateType(), new WireType(), new CableType() })
                .Concat(HouseType.Types())
                .ToList());

        private static readonly Lazy<IReadOnlyDictionary<string, ObjectType>> ById = new(() => AllTypes.Value.ToDictionary(t => t.Id, StringComparer.Ordinal));

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "switchgear";
        public I18nText Name => new("Switchgear & installation", "Verteiler & Installation");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        /// <summary>The module's type for an id, else null.</summary>
        internal static ObjectType? TypeOf(string id) => ById.Value.TryGetValue(id, out var t) ? t : null;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(SwCategories.Protection, new I18nText("Protection & isolation", "Schutz- & Trenngeräte"), Id, 10),
            new LibraryCategory(SwCategories.Switching, new I18nText("Switching & control (DIN rail)", "Schalten & Steuern (Hutschiene)"), Id, 20),
            new LibraryCategory(SwCategories.Supply, new I18nText("Meters, supplies & bus devices", "Zähler, Netzteile & Busgeräte"), Id, 30),
            new LibraryCategory(SwCategories.Automation, new I18nText("Drives & automation (industrial)", "Antriebe & Automatisierung (Industrie)"), Id, 35),
            new LibraryCategory(SwCategories.Terminals, new I18nText("Terminal blocks", "Reihenklemmen"), Id, 40),
            new LibraryCategory(SwCategories.Mounting, new I18nText("Rails, ducts & labels", "Schienen, Kanäle & Beschriftung"), Id, 50),
            new LibraryCategory(SwCategories.Wiring, new I18nText("Wires & cables", "Adern & Kabel"), Id, 60),
            new LibraryCategory(SwCategories.House, new I18nText("Installation devices (rooms)", "Installationsgeräte (Räume)"), Id, 70),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        /// <summary>The installation reports, each as CSV and as Markdown (<see cref="IExportProvider"/>).</summary>
        public IEnumerable<IExporter> Exporters
        {
            get
            {
                foreach (SwReport r in Enum.GetValues(typeof(SwReport)))
                {
                    yield return new SwExporter(r, markdown: false);
                    yield return new SwExporter(r, markdown: true);
                }
            }
        }

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();
            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            void Add(string id, string type, string en, string de, IReadOnlyDictionary<string, ParamValue>? ps = null, string? text = null, Size2? size = null,
                IReadOnlyList<Vec2>? points = null, params string[] kw)
            {
                var t = types[type];
                var item = new LibraryItem(id, type, new I18nText(en, de), t.Category) { Params = ps, Text = text, Keywords = kw, Points = points };
                if (t.Geometry == GeometryKind.Box)
                {
                    var probe = t.Create(Vec2.Zero, item);
                    item = item with { Size = size ?? t.NaturalSize(probe) ?? t.DefaultSize, View = t.Views.Count > 0 ? t.DefaultView : null };
                }
                list.Add(item);
            }

            // Protection & isolation.
            Add("sw.protection.mcb", Devices.Mcb, "MCB B16, 1-pole", "Leitungsschutzschalter B16, 1-polig", kw: new[] { "ls", "b16" });
            Add("sw.protection.mcb.b10", Devices.Mcb, "MCB B10, 1-pole", "Leitungsschutzschalter B10, 1-polig", P(("rating", "10")), kw: new[] { "ls", "b10", "licht" });
            Add("sw.protection.mcb.c16.3p", Devices.Mcb, "MCB C16, 3-pole", "Leitungsschutzschalter C16, 3-polig", P(("char", "C"), ("poles", "3")), kw: new[] { "ls", "c16", "drehstrom" });
            Add("sw.protection.mcb.b16.1n", Devices.Mcb, "MCB B16, 1+N", "Leitungsschutzschalter B16, 1+N", P(("poles", "1N")), kw: new[] { "ls", "1+n" });
            Add("sw.protection.rcd", Devices.Rcd, "RCD 40 A 30 mA, 2-pole", "FI-Schutzschalter 40 A 30 mA, 2-polig", kw: new[] { "fi", "rcd" });
            Add("sw.protection.rcd.4p", Devices.Rcd, "RCD 40 A 30 mA, 4-pole", "FI-Schutzschalter 40 A 30 mA, 4-polig", P(("poles", "4")), kw: new[] { "fi", "rcd", "4-polig" });
            Add("sw.protection.rcd.4p.b", Devices.Rcd, "RCD 63 A 30 mA type B, 4-pole", "FI-Schutzschalter 63 A 30 mA Typ B, 4-polig", P(("poles", "4"), ("rating", "63"), ("rcdType", "B")),
                kw: new[] { "fi", "typ b", "wallbox", "allstromsensitiv" });
            Add("sw.protection.rcbo", Devices.Rcbo, "RCBO B16 30 mA, 1+N", "FI/LS-Schalter B16 30 mA, 1+N", kw: new[] { "fi/ls", "rcbo" });
            Add("sw.protection.spd", Devices.Spd, "SPD type 2, 3+N", "Überspannungsschutz Typ 2, 3+N", kw: new[] { "spd", "typ 2" });
            Add("sw.protection.spd.t12", Devices.Spd, "SPD type 1+2, 3+N", "Überspannungsschutz Typ 1+2, 3+N", P(("spdType", "12")), kw: new[] { "spd", "kombiableiter", "blitzstrom" });
            Add("sw.switch.main", Devices.MainSwitch, "Main switch 63 A, 3-pole", "Hauptschalter 63 A, 3-polig", kw: new[] { "hauptschalter", "trennschalter" });
            Add("sw.protection.mpcb", Industrial.Mpcb, "Motor protection circuit breaker 4 A", "Motorschutzschalter 4 A", kw: new[] { "motorschutzschalter", "mpcb" });
            Add("sw.protection.mpcb.10", Industrial.Mpcb, "Motor protection circuit breaker 10 A", "Motorschutzschalter 10 A", P(("rating", "10")), kw: new[] { "motorschutzschalter", "mpcb" });
            Add("sw.protection.overload", Industrial.Overload, "Thermal overload relay 4 A", "Überlastrelais 4 A", kw: new[] { "überlastrelais", "motorschutzrelais" });
            Add("sw.fuse.nh", Industrial.NhFuse, "NH00 fuse switch-disconnector 63 A", "NH00-Sicherungslasttrenner 63 A", kw: new[] { "nh", "lasttrenner", "hauptsicherung" });
            Add("sw.fuse.nh.000.35", Industrial.NhFuse, "NH000 fuse switch-disconnector 35 A", "NH000-Sicherungslasttrenner 35 A", P(("size", "000"), ("rating", "35")),
                kw: new[] { "nh", "lasttrenner", "hausanschluss", "vorsicherung" });

            // Switching & control.
            Add("sw.switching.contactor", Devices.Contactor, "Installation contactor 2 NO", "Installationsschütz 2 Schließer", kw: new[] { "schütz" });
            Add("sw.switching.contactor.4", Devices.Contactor, "Installation contactor 4 NO", "Installationsschütz 4 Schließer", P(("contacts", "4")), kw: new[] { "schütz" });
            Add("sw.switching.relay", Devices.Relay, "Coupling relay 1 changeover", "Koppelrelais 1 Wechsler", kw: new[] { "relais" });
            Add("sw.switching.impulse", Devices.Impulse, "Impulse relay 1 NO", "Stromstoßschalter 1 Schließer", kw: new[] { "stromstoß" });
            Add("sw.switching.timer", Devices.Timer, "Timer relay (on-delay)", "Zeitrelais (ansprechverzögert)", kw: new[] { "zeitrelais" });
            Add("sw.switching.timer.staircase", Devices.Timer, "Staircase timer", "Treppenlichtzeitschalter", P(("function", "staircase"), ("time", "3 min")), kw: new[] { "treppenlicht", "staircase" });
            Add("sw.control.pushbutton", Devices.PushButton, "Push button for DIN rail (NO)", "Taster für Hutschiene (Schließer)", kw: new[] { "taster" });
            Add("sw.control.switch", Devices.Switch, "Switch for DIN rail (0-I)", "Schalter für Hutschiene (0-I)", kw: new[] { "schalter" });
            Add("sw.control.switch.selector", Devices.Switch, "Selector switch for DIN rail (I-0-II)", "Wahlschalter für Hutschiene (I-0-II)", P(("kind", "selector")), kw: new[] { "wahlschalter", "hand-0-auto" });
            Add("sw.control.lamp", Devices.Lamp, "Indicator lamp for DIN rail (green)", "Leuchtmelder für Hutschiene (grün)", kw: new[] { "leuchtmelder" });
            Add("sw.control.lamp.red", Devices.Lamp, "Indicator lamp for DIN rail (red)", "Leuchtmelder für Hutschiene (rot)", P(("colour", "red")), kw: new[] { "leuchtmelder", "störung" });
            Add("sw.socket.dinrail", Devices.Socket, "Socket outlet for DIN rail", "Steckdose für Hutschiene", kw: new[] { "steckdose" });

            // Meters, supplies & bus devices.
            Add("sw.meter.energy", Devices.Meter, "Energy meter, single-phase (DIN rail)", "Wechselstromzähler (Hutschiene)", kw: new[] { "zähler" });
            Add("sw.meter.energy.3", Devices.Meter, "Energy meter, three-phase (DIN rail)", "Drehstromzähler (Hutschiene)", P(("phases", "3")), kw: new[] { "zähler", "drehstrom" });
            Add("sw.supply.psu", Devices.Psu, "DIN-rail power supply 24 V 60 W", "Hutschienennetzteil 24 V 60 W", kw: new[] { "netzteil" });
            Add("sw.supply.psu.12v", Devices.Psu, "DIN-rail power supply 12 V 30 W", "Hutschienennetzteil 12 V 30 W", P(("voltage", "12"), ("power", "30")), kw: new[] { "netzteil", "12 v" });
            Add("sw.bus.actuator", Devices.BusActuator, "KNX switch actuator, 4-fold", "KNX-Schaltaktor, 4-fach", kw: new[] { "knx", "schaltaktor" });
            Add("sw.bus.actuator.8", Devices.BusActuator, "KNX switch actuator, 8-fold", "KNX-Schaltaktor, 8-fach", P(("channels", "8")), kw: new[] { "knx", "schaltaktor" });
            Add("sw.bus.actuator.dimmer", Devices.BusActuator, "KNX dimming actuator, 2-fold", "KNX-Dimmaktor, 2-fach", P(("function", "dimmer"), ("channels", "2")), kw: new[] { "knx", "dimmaktor" });
            Add("sw.bus.actuator.blind", Devices.BusActuator, "KNX blind actuator, 4-fold", "KNX-Jalousieaktor, 4-fach", P(("function", "blind")), kw: new[] { "knx", "jalousieaktor", "rollladen" });
            Add("sw.bus.actuator.input", Devices.BusActuator, "KNX binary input, 4-fold", "KNX-Binäreingang, 4-fach", P(("function", "input")), kw: new[] { "knx", "binäreingang" });
            Add("sw.bus.coupler", Devices.BusCoupler, "KNX line coupler", "KNX-Linienkoppler", kw: new[] { "knx", "linienkoppler" });
            Add("sw.bus.power", Devices.BusPower, "KNX power supply 640 mA", "KNX-Spannungsversorgung 640 mA", kw: new[] { "knx", "spannungsversorgung" });
            Add("sw.bus.actuator.generic", Devices.BusActuator, "Bus actuator, 4 channels (generic)", "Busaktor, 4 Kanäle (allgemein)", P(("bus", "generic")), kw: new[] { "bus", "aktor", "modbus", "dali" });
            Add("sw.bus.power.generic", Devices.BusPower, "Bus power supply (generic)", "Busnetzteil (allgemein)", P(("bus", "generic")), kw: new[] { "bus" });

            // Drives & automation.
            Add("sw.drive.vfd", Industrial.Vfd, "Frequency inverter 0.75 kW, 3~", "Frequenzumrichter 0,75 kW, 3~", kw: new[] { "frequenzumrichter", "fu", "vfd" });
            Add("sw.drive.vfd.1ph", Industrial.Vfd, "Frequency inverter 0.37 kW, 1~ 230 V", "Frequenzumrichter 0,37 kW, 1~ 230 V", P(("power", "0.37"), ("supply", "1")),
                kw: new[] { "frequenzumrichter", "fu", "einphasig" });
            Add("sw.drive.vfd.4kw", Industrial.Vfd, "Frequency inverter 4 kW, 3~", "Frequenzumrichter 4 kW, 3~", P(("power", "4")), kw: new[] { "frequenzumrichter", "fu", "vfd" });
            Add("sw.plc.module", Industrial.Plc, "PLC digital input module, 8 channels", "SPS-Digitaleingabe, 8 Kanäle", kw: new[] { "sps", "plc", "di" });
            Add("sw.plc.module.cpu", Industrial.Plc, "PLC CPU", "SPS-CPU", P(("kind", "cpu")), kw: new[] { "sps", "plc", "cpu", "steuerung" });
            Add("sw.plc.module.do", Industrial.Plc, "PLC digital output module, 8 channels", "SPS-Digitalausgabe, 8 Kanäle", P(("kind", "do")), kw: new[] { "sps", "plc", "do" });
            Add("sw.plc.module.ai", Industrial.Plc, "PLC analogue input module, 8 channels", "SPS-Analogeingabe, 8 Kanäle", P(("kind", "ai")), kw: new[] { "sps", "plc", "ai", "analog" });
            Add("sw.safety.relay", Industrial.SafetyRelay, "Safety relay (emergency stop)", "Sicherheitsrelais (Not-Halt)", kw: new[] { "sicherheitsrelais", "not-halt" });
            Add("sw.safety.relay.door", Industrial.SafetyRelay, "Safety relay (guard door)", "Sicherheitsrelais (Schutztür)", P(("function", "door")), kw: new[] { "sicherheitsrelais", "schutztür" });

            // Terminal blocks.
            Add("sw.terminal.feedthrough", Terminals.FeedThrough, "Feed-through terminal 2.5 mm²", "Durchgangsklemme 2,5 mm²", kw: new[] { "klemme" });
            Add("sw.terminal.feedthrough.4", Terminals.FeedThrough, "Feed-through terminal 4 mm²", "Durchgangsklemme 4 mm²", P(("section", "4")), kw: new[] { "klemme" });
            Add("sw.terminal.feedthrough.10", Terminals.FeedThrough, "Feed-through terminal 10 mm²", "Durchgangsklemme 10 mm²", P(("section", "10")), kw: new[] { "klemme", "einspeisung" });
            Add("sw.terminal.feedthrough.bridged", Terminals.FeedThrough, "Feed-through terminal with bridge", "Durchgangsklemme mit Brücke", P(("bridge", true)), kw: new[] { "brücke", "gebrückt" });
            Add("sw.terminal.n", Terminals.Neutral, "Neutral terminal 2.5 mm² (blue)", "N-Klemme 2,5 mm² (blau)", kw: new[] { "n-klemme" });
            Add("sw.terminal.pe", Terminals.Earth, "PE terminal 2.5 mm²", "PE-Klemme 2,5 mm²", kw: new[] { "schutzleiterklemme" });
            Add("sw.terminal.pe.10", Terminals.Earth, "PE terminal 10 mm²", "PE-Klemme 10 mm²", P(("section", "10")), kw: new[] { "schutzleiterklemme", "potentialausgleich" });
            Add("sw.terminal.fused", Terminals.Fused, "Fused terminal 5×20 (6.3 A)", "Sicherungsklemme 5×20 (6,3 A)", kw: new[] { "sicherungsklemme" });
            Add("sw.terminal.multilevel", Terminals.MultiLevel, "Double-level terminal 2.5 mm²", "Doppelstockklemme 2,5 mm²", kw: new[] { "doppelstock" });
            Add("sw.terminal.multilevel.3", Terminals.MultiLevel, "Triple-level terminal 2.5 mm²", "Dreistockklemme 2,5 mm²", P(("levels", "3")), kw: new[] { "dreistock", "etagenklemme" });
            Add("sw.terminal.bridge", Accessories.BridgeId, "Plug-in bridge, 2-pole", "Steckbrücke, 2-polig", kw: new[] { "brücke" });
            Add("sw.terminal.bridge.5", Accessories.BridgeId, "Plug-in bridge, 5-pole", "Steckbrücke, 5-polig", P(("positions", 5)), kw: new[] { "brücke" });

            // Rails, ducts & labels.
            Add("sw.rail.ts35", Rails.RailId, "DIN rail TS35 (12 modules)", "Hutschiene TS35 (12 TE)", kw: new[] { "hutschiene" });
            Add("sw.rail.ts35.24", Rails.RailId, "DIN rail TS35 (24 modules)", "Hutschiene TS35 (24 TE)", size: new Size2(24 * Modular.Unit + 15, Modular.RailHeight), kw: new[] { "hutschiene" });
            Add("sw.rail.endclamp", Accessories.EndClampId, "End clamp", "Endhalter", kw: new[] { "endhalter" });
            Add("sw.busbar.comb", Industrial.CombBusbar, "Comb busbar 3-phase (12 modules)", "Phasenschiene 3-phasig (12 TE)", kw: new[] { "phasenschiene", "kammschiene" });
            Add("sw.busbar.comb.1n", Industrial.CombBusbar, "Comb busbar 1-phase + N (12 modules)", "Phasenschiene 1-phasig + N (12 TE)", P(("phases", "1N")), kw: new[] { "phasenschiene", "kammschiene", "fi/ls" });
            Add("sw.blank", Industrial.Blank, "Blanking plate (1 module)", "Abdeckstreifen (1 TE)", kw: new[] { "abdeckstreifen", "blindabdeckung" });
            Add("sw.blank.6", Industrial.Blank, "Blanking plate (6 modules)", "Abdeckstreifen (6 TE)", P(("modules", 6)), kw: new[] { "abdeckstreifen", "blindabdeckung" });
            Add("sw.duct.slotted", Rails.DuctId, "Slotted cable duct (horizontal)", "Verdrahtungskanal (waagerecht)", kw: new[] { "kanal" });
            Add("sw.duct.slotted.vertical", Rails.DuctId, "Slotted cable duct (vertical)", "Verdrahtungskanal (senkrecht)", size: new Size2(40, 12 * Modular.Unit + 15), kw: new[] { "kanal", "senkrecht" });
            Add("sw.label.strip", Labels.StripId, "Labelling strip (reads the devices)", "Beschriftungsstreifen (liest die Geräte)", kw: new[] { "beschriftung" });
            Add("sw.label.plate", Labels.PlateId, "Label plate", "Bezeichnungsschild", text: "-Q1", kw: new[] { "schild" });

            // Wires & cables.
            var pts = new[] { new Vec2(-15, 0), new Vec2(15, 0) };
            Add("sw.line.wire", Lines.WireId, "Wire, black (cabinet)", "Ader schwarz (Schaltschrank)", points: pts, kw: new[] { "ader", "verdrahtung", "l" });
            Add("sw.line.wire.bn", Lines.WireId, "Wire, brown (L)", "Ader braun (L)", P(("colour", "BN")), points: pts, kw: new[] { "ader", "außenleiter", "phase" });
            Add("sw.line.wire.bu", Lines.WireId, "Wire, blue (N)", "Ader blau (N)", P(("colour", "BU")), points: pts, kw: new[] { "ader", "neutralleiter", "n" });
            Add("sw.line.wire.pe", Lines.WireId, "Wire, green-yellow (PE)", "Ader grün-gelb (PE)", P(("colour", "GNYE")), points: pts, kw: new[] { "ader", "schutzleiter", "pe" });
            Add("sw.line.cable", Lines.CableId, "Cable NYM-J 3×1.5", "Kabel NYM-J 3×1,5", points: pts, kw: new[] { "nym", "mantelleitung" });
            Add("sw.line.cable.5x25", Lines.CableId, "Cable NYM-J 5×2.5", "Kabel NYM-J 5×2,5", P(("cores", "5"), ("section", "2.5")), points: pts, kw: new[] { "nym", "drehstrom", "herd" });
            Add(Lines.BusCableItem, Lines.CableId, "Bus cable J-Y(ST)Y 2×2×0.8", "Buskabel J-Y(ST)Y 2×2×0,8", P(("cableType", "jysty"), ("cores", "4"), ("section", "0.8")), points: pts,
                kw: new[] { "knx", "bus", "eib", "busleitung" });

            // Installation devices in the rooms.
            Add("sw.house.socket", HouseType.SocketId, "Socket outlet (room/circuit)", "Steckdose (Raum/Stromkreis)", kw: new[] { "steckdose" });
            Add("sw.house.socket.double", HouseType.SocketId, "Double socket outlet (room/circuit)", "Zweifachsteckdose (Raum/Stromkreis)", P(("gangs", "2")), kw: new[] { "doppelsteckdose" });
            Add("sw.house.switch", HouseType.SwitchId, "Light switch (room/circuit)", "Lichtschalter (Raum/Stromkreis)", kw: new[] { "ausschalter" });
            Add("sw.house.switch.twoway", HouseType.SwitchId, "Two-way light switch (room/circuit)", "Wechsel-Lichtschalter (Raum/Stromkreis)", P(("kind", "twoway")), kw: new[] { "wechselschalter" });
            Add("sw.house.switch.push", HouseType.SwitchId, "Push button (room/circuit)", "Taster (Raum/Stromkreis)", P(("kind", "push")), kw: new[] { "taster" });
            Add("sw.house.light", HouseType.LightId, "Ceiling light outlet (room/circuit)", "Deckenauslass (Raum/Stromkreis)", kw: new[] { "leuchte", "deckenauslass" });
            Add("sw.house.light.wall", HouseType.LightId, "Wall light outlet (room/circuit)", "Wandauslass (Raum/Stromkreis)", P(("kind", "wall")), kw: new[] { "leuchte", "wandauslass" });
            Add("sw.house.appliance", HouseType.ApplianceId, "Cooker connection (room/circuit)", "Herdanschluss (Raum/Stromkreis)", kw: new[] { "herd" });
            Add("sw.house.appliance.washer", HouseType.ApplianceId, "Washing machine connection (room/circuit)", "Waschmaschinenanschluss (Raum/Stromkreis)", P(("kind", "washer")), kw: new[] { "waschmaschine" });
            return list;
        }
    }
}
