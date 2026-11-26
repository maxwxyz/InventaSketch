using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// Electrical &amp; electronics module: IEC 60617-style circuit symbols (drawn independently), wires with
    /// free/orthogonal/45 degree routing, junction dots and net labels, house installation plan symbols, single-line
    /// diagram elements, power electronics blocks and illustrative PCB objects. Every symbol is insertable without
    /// parameters, drawn inside its box and exposes its pins as <see cref="AnchorRole.Port"/> anchors.
    /// This is a sketching module, not an ECAD/ERC tool.
    /// </summary>
    public sealed class ElectricalModule : IModule, IExportProvider
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() =>
            Passive.Types().Cast<ObjectType>()
                .Concat(Semiconductors.Types())
                .Concat(Switching.Types())
                .Concat(Sources.Types())
                .Concat(Logic.Types())
                .Concat(Connectors.Types())
                .Concat(new ObjectType[] { new WireType(), new BusType() })
                .Concat(House.Types())
                .Concat(Power.Types())
                .Concat(Pcb.Types())
                .ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        private static readonly Lazy<IReadOnlyDictionary<string, ObjectType>> ById = new(() => AllTypes.Value.ToDictionary(t => t.Id, StringComparer.Ordinal));

        /// <summary>A type of this module by id (the circuit analysis reads pins and roles without a registry).</summary>
        internal static ObjectType? FindType(string id) => ById.Value.TryGetValue(id, out var t) ? t : null;

        public string Id => "electrical";
        public I18nText Name => new("Electrical & electronics", "Elektrotechnik & Elektronik");
        public string Version => "0.2.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            // Most used first: sources and loads (ground, battery, lamp, motor) and wires, then passive parts and switching.
            new LibraryCategory(Passive.Cat, new I18nText("Passive components", "Passive Bauelemente"), Id, 20),
            new LibraryCategory(Semiconductors.Cat, new I18nText("Semiconductors", "Halbleiter"), Id, 40),
            new LibraryCategory(Switching.Cat, new I18nText("Switching & protection", "Schalten & Schützen"), Id, 30),
            new LibraryCategory(Sources.Cat, new I18nText("Sources & loads", "Quellen & Verbraucher"), Id, 10),
            new LibraryCategory(Logic.Cat, new I18nText("Logic & ICs", "Logik & ICs"), Id, 50),
            new LibraryCategory(Connectors.Cat, new I18nText("Wires, connectors & buses", "Leitungen, Steckverbinder & Busse"), Id, 15),
            new LibraryCategory(House.Cat, new I18nText("House installation", "Hausinstallation"), Id, 70),
            new LibraryCategory(Power.Cat, new I18nText("Power electronics", "Leistungselektronik"), Id, 80),
            new LibraryCategory(Pcb.Cat, new I18nText("PCB (illustrative)", "Leiterplatte (illustrativ)"), Id, 90),
        };

        /// <summary>Domain exports of this module (<see cref="IExportProvider"/>).</summary>
        public IEnumerable<IExporter> Exporters => new IExporter[] { new NetlistExporter(), new ErcExporter() };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();

            void Add(string id, string type, string en, string de, string? view = null, IReadOnlyDictionary<string, ParamValue>? ps = null, string? text = null, Size2? size = null, params string[] kw)
            {
                var t = types[type];
                var item = new LibraryItem("elec." + id, type, new I18nText(en, de), t.Category) { Params = ps, Text = text, Keywords = kw };
                if (t is ElecType et)
                {
                    var spec = et.Spec(view);
                    item = item with { View = spec.Id, Size = size ?? spec.Design };
                }
                list.Add(item);
            }

            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            // passive
            Add("resistor", "elec.resistor", "Resistor", "Widerstand");
            Add("resistor.ansi", "elec.resistor", "Resistor (ANSI zigzag)", "Widerstand (ANSI-Zickzack)", "ansi", null, null, null, "zigzag", "ansi", "us");
            Add("potentiometer", "elec.potentiometer", "Potentiometer", "Potentiometer");
            Add("resistor.ntc", "elec.resistor.thermistor", "NTC thermistor", "Heißleiter (NTC)", null, P(("coefficient", "ntc")), null, null, "ntc", "heißleiter");
            Add("resistor.ptc", "elec.resistor.thermistor", "PTC thermistor", "Kaltleiter (PTC)", null, P(("coefficient", "ptc")), null, null, "ptc", "kaltleiter");
            Add("resistor.ldr", "elec.resistor.ldr", "Photoresistor (LDR)", "Fotowiderstand (LDR)");
            Add("resistor.varistor", "elec.resistor.varistor", "Varistor (VDR)", "Varistor (VDR)");
            Add("capacitor", "elec.capacitor", "Capacitor", "Kondensator");
            Add("capacitor.polarized", "elec.capacitor.polarized", "Electrolytic capacitor", "Elektrolytkondensator");
            Add("inductor", "elec.inductor", "Inductor", "Spule");
            Add("inductor.iron", "elec.inductor", "Inductor with iron core", "Spule mit Eisenkern", null, P(("core", "iron")), null, null, "choke", "drossel");
            Add("transformer", "elec.transformer", "Transformer", "Transformator");
            Add("transformer.circles", "elec.transformer", "Transformer (single-line)", "Transformator (einpolig)", "circles", null, null, null, "single-line", "einpolig");
            Add("crystal", "elec.crystal", "Quartz crystal", "Schwingquarz (Quarz)");

            // semiconductors
            Add("diode", "elec.diode", "Diode", "Diode");
            Add("led", "elec.diode.led", "LED", "Leuchtdiode (LED)");
            Add("zener", "elec.diode.zener", "Zener diode", "Z-Diode");
            Add("photodiode", "elec.diode.photo", "Photodiode", "Fotodiode");
            Add("schottky", "elec.diode.schottky", "Schottky diode", "Schottky-Diode");
            Add("tvs", "elec.diode.tvs", "TVS diode (unidirectional)", "Suppressordiode (unidirektional)");
            Add("tvs.bi", "elec.diode.tvs", "TVS diode (bidirectional)", "Suppressordiode (bidirektional)", null, P(("direction", "bi")), null, null, "bidirectional", "bidirektional");
            Add("bridge", "elec.diode.bridge", "Bridge rectifier", "Brückengleichrichter");
            Add("npn", "elec.transistor.bjt", "NPN transistor", "NPN-Transistor", null, P(("polarity", "npn")), null, null, "npn");
            Add("pnp", "elec.transistor.bjt", "PNP transistor", "PNP-Transistor", null, P(("polarity", "pnp")), null, null, "pnp");
            Add("nmos", "elec.transistor.mosfet", "N-channel MOSFET", "N-Kanal-MOSFET", null, P(("channel", "n")), null, null, "nmos", "n-channel");
            Add("pmos", "elec.transistor.mosfet", "P-channel MOSFET", "P-Kanal-MOSFET", null, P(("channel", "p")), null, null, "pmos", "p-channel");
            Add("njfet", "elec.transistor.jfet", "N-channel JFET", "N-Kanal-JFET", null, P(("channel", "n")), null, null, "njfet", "n-channel");
            Add("pjfet", "elec.transistor.jfet", "P-channel JFET", "P-Kanal-JFET", null, P(("channel", "p")), null, null, "pjfet", "p-channel");
            Add("igbt", "elec.transistor.igbt", "IGBT", "IGBT");
            Add("thyristor", "elec.thyristor", "Thyristor", "Thyristor");
            Add("triac", "elec.triac", "Triac", "Triac");
            Add("diac", "elec.diac", "Diac", "Diac");
            Add("optocoupler", "elec.optocoupler", "Optocoupler", "Optokoppler");

            // switching & protection
            Add("switch.no", "elec.switch", "Switch (normally open)", "Schalter (Schließer)", null, P(("contact", "no")), null, null, "make", "schließer");
            Add("switch.nc", "elec.switch", "Switch (normally closed)", "Schalter (Öffner)", null, P(("contact", "nc")), null, null, "break", "öffner");
            Add("switch.changeover", "elec.switch", "Changeover switch", "Umschalter (Wechsler)", null, P(("contact", "changeover")), null, null, "changeover", "wechsler", "umschalter");
            Add("pushbutton", "elec.pushbutton", "Push button (NO)", "Taster (Schließer)");
            Add("pushbutton.nc", "elec.pushbutton", "Push button (NC)", "Taster (Öffner)", null, P(("contact", "nc")));
            Add("relay.coil", "elec.relay.coil", "Relay coil", "Relaisspule");
            Add("relay.contact.no", "elec.switch", "Relay contact (NO)", "Relaiskontakt (Schließer)", null, P(("contact", "no")), null, null, "relay", "relais");
            Add("relay.contact.nc", "elec.switch", "Relay contact (NC)", "Relaiskontakt (Öffner)", null, P(("contact", "nc")), null, null, "relay", "relais");
            Add("contactor", "elec.contactor", "Contactor, 3-pole", "Schütz, 3-polig");
            Add("fuse", "elec.fuse", "Fuse", "Sicherung");
            Add("breaker", "elec.breaker", "Miniature circuit breaker", "Leitungsschutzschalter");
            Add("rcd", "elec.rcd", "RCD (residual current device)", "FI-Schutzschalter (RCD)");

            // sources & loads
            Add("ground", "elec.ground", "Ground / earth", "Erde / Masse");
            Add("ground.pe", "elec.ground", "Protective earth (PE)", "Schutzerde (PE)", null, P(("kind", "pe")), null, null, "pe", "schutzleiter");
            Add("ground.chassis", "elec.ground", "Chassis ground", "Gehäusemasse", null, P(("kind", "chassis")), null, null, "chassis", "frame", "karosserie");
            Add("source.dc", "elec.source", "DC voltage source", "Gleichspannungsquelle", null, P(("kind", "dc")));
            Add("source.ac", "elec.source", "AC voltage source", "Wechselspannungsquelle", null, P(("kind", "ac")));
            Add("source.current", "elec.source", "Current source", "Stromquelle", null, P(("kind", "current")));
            Add("battery.cell", "elec.battery", "Battery cell", "Batteriezelle", null, P(("cells", 1)));
            Add("battery.pack", "elec.battery", "Battery pack", "Batteriepack", null, P(("cells", 3)), null, null, "pack", "hv", "traction battery", "traktionsbatterie");
            Add("lamp", "elec.lamp", "Lamp", "Lampe");
            Add("motor", "elec.motor", "Motor", "Motor");
            Add("motor.dc", "elec.motor", "DC motor", "Gleichstrommotor", null, P(("supply", "dc")));
            Add("motor.ac", "elec.motor", "AC motor", "Wechselstrommotor", null, P(("supply", "ac")));
            Add("motor.3ph", "elec.motor", "Three-phase motor", "Drehstrommotor", null, P(("supply", "ac3")), null, null, "3~", "three-phase", "drehstrom");
            Add("generator", "elec.generator", "Generator", "Generator");
            Add("generator.3ph", "elec.generator", "Three-phase generator", "Drehstromgenerator", null, P(("supply", "ac3")), null, null, "3~", "alternator", "drehstrom");
            Add("sensor", "elec.sensor", "Sensor", "Sensor");
            Add("sensor.temperature", "elec.sensor", "Temperature sensor", "Temperatursensor", null, P(("label", "T")), null, null, "temperature", "temperatur", "ntc");
            Add("actuator", "elec.actuator", "Actuator", "Aktor");
            Add("solenoid", "elec.solenoid", "Solenoid", "Hubmagnet");
            Add("meter.v", "elec.meter", "Voltmeter", "Spannungsmesser", null, P(("quantity", "v")), null, null, "voltmeter", "spannungsmesser");
            Add("meter.a", "elec.meter", "Ammeter", "Strommesser", null, P(("quantity", "a")), null, null, "ammeter", "strommesser", "amperemeter");
            Add("meter.w", "elec.meter", "Wattmeter", "Leistungsmesser", null, P(("quantity", "w")), null, null, "wattmeter", "leistungsmesser");
            Add("meter.ohm", "elec.meter", "Ohmmeter", "Widerstandsmesser", null, P(("quantity", "ohm")), null, null, "ohmmeter", "widerstandsmesser");
            Add("speaker", "elec.speaker", "Loudspeaker", "Lautsprecher");
            Add("microphone", "elec.microphone", "Microphone", "Mikrofon");
            Add("antenna", "elec.antenna", "Antenna", "Antenne");

            // logic & ICs
            foreach (var (g, en, de) in new[] { ("and", "AND", "UND"), ("or", "OR", "ODER"), ("not", "NOT", "NICHT"), ("nand", "NAND", "NAND"), ("nor", "NOR", "NOR"), ("xor", "XOR", "XOR") })
            {
                Add("gate." + g, "elec.gate", $"{en} gate (IEC)", $"{de}-Gatter (IEC)", "symbol", P(("gate", g)), null, null, g);
                Add("gate." + g + ".ansi", "elec.gate", $"{en} gate (ANSI)", $"{de}-Gatter (ANSI)", "ansi", P(("gate", g)), null, null, g, "ansi", "distinctive");
            }
            Add("flipflop.d", "elec.flipflop", "D flip-flop", "D-Flipflop", null, P(("kind", "d")));
            Add("flipflop.jk", "elec.flipflop", "JK flip-flop", "JK-Flipflop", null, P(("kind", "jk")));
            Add("flipflop.sr", "elec.flipflop", "SR latch", "RS-Flipflop", null, P(("kind", "sr")));
            Add("opamp", "elec.opamp", "Op-amp (IEC)", "Operationsverstärker (IEC)");
            Add("opamp.ansi", "elec.opamp", "Op-amp (triangle)", "Operationsverstärker (Dreieck)", "ansi", null, null, null, "triangle", "dreieck");
            Add("regulator", "elec.regulator", "Voltage regulator", "Spannungsregler");
            Add("ecu", "elec.block.ecu", "Control unit (ECU)", "Steuergerät (ECU)");
            Add("ic", "elec.block.ecu", "IC (8 pins)", "IC (8 Pins)", null, P(("label", "IC"), ("pinsLeft", 4), ("pinsRight", 4)), null, new Size2(30, 25), "chip", "ic");
            Add("mcu", "elec.block.ecu", "Microcontroller", "Mikrocontroller", null, P(("label", "MCU"), ("pinsLeft", 6), ("pinsRight", 6)), null, new Size2(30, 35), "mcu", "µc");

            // wires, connectors & buses
            Add("wire", "elec.wire", "Wire", "Leitung");
            Add("wire.free", "elec.wire", "Wire (straight)", "Leitung (gerade)", null, P(("route", "free")));
            Add("junction", "elec.junction", "Junction dot (circuit)", "Verbindungspunkt (Schaltplan)");
            Add("netlabel", "elec.netlabel", "Net label", "Netzname");
            Add("netlabel.vcc", "elec.netlabel", "Net label VCC", "Netzname VCC", null, null, "VCC", null, "supply", "versorgung");
            Add("netlabel.gnd", "elec.netlabel", "Net label GND", "Netzname GND", null, null, "GND", null, "ground", "masse");
            Add("connector", "elec.connector", "Plug and socket", "Steckverbindung");
            Add("connector.plug", "elec.connector", "Plug", "Stecker", null, P(("gender", "plug")));
            Add("connector.socket", "elec.connector", "Socket", "Buchse", null, P(("gender", "socket")));
            Add("connector.4", "elec.connector", "Connector, 4-pole", "Steckverbinder, 4-polig", null, P(("poles", 4)), null, new Size2(20, 20), "multi-pole", "mehrpolig");
            Add("terminal", "elec.terminal", "Terminal", "Anschlussklemme");
            Add("testpoint", "elec.testpoint", "Test point", "Messpunkt");
            Add("bus.can", "elec.bus", "CAN bus", "CAN-Bus", null, P(("protocol", "can")), null, null, "can");
            Add("bus.lin", "elec.bus", "LIN bus", "LIN-Bus", null, P(("protocol", "lin")), null, null, "lin");
            Add("bus.ethernet", "elec.bus", "Automotive Ethernet", "Automotive Ethernet", null, P(("protocol", "ethernet")), null, null, "ethernet");

            // house installation & single-line: the canonical room devices (the switchgear module's room devices give way to
            // them while this module is registered, LibraryItem.Alternative), so they also answer to the switchgear names.
            Add("house.socket", "elec.house.socket", "Socket outlet (earthed)", "Schutzkontaktsteckdose", null, null, null, null, "room", "raum", "stromkreis");
            Add("house.socket.double", "elec.house.socket", "Double socket outlet", "Doppelsteckdose", null, P(("gangs", 2)), null, null, "double", "doppel", "zweifachsteckdose");
            Add("house.socket.plain", "elec.house.socket", "Socket outlet (no earth)", "Steckdose ohne Schutzkontakt", null, P(("protected", false)));
            Add("house.switch", "elec.house.switch", "One-way switch", "Ausschalter", null, null, null, null, "light switch", "lichtschalter");
            Add("house.switch.twoway", "elec.house.switch", "Two-way switch", "Wechselschalter", null, P(("kind", "twoway")), null, null, "two-way", "wechsel", "wechsel-lichtschalter");
            Add("house.switch.push", "elec.house.switch", "Push button (plan)", "Taster (Installationsplan)", null, P(("kind", "push")), null, null, "push", "taster");
            Add("house.light", "elec.house.light", "Luminaire", "Leuchte", null, null, null, null, "light outlet", "ceiling light outlet", "leuchtenauslass", "deckenauslass", "brennstelle");
            Add("house.junction", "elec.house.junction", "Junction box", "Abzweigdose");
            Add("house.board", "elec.house.board", "Distribution board", "Verteiler");
            Add("house.cable", "elec.house.cable", "Cable route", "Leitungsführung");
            Add("sld.busbar", "elec.sld.busbar", "Busbar", "Sammelschiene");
            Add("sld.feeder", "elec.sld.feeder", "Feeder with circuit breaker", "Abgang mit Leistungsschalter");
            Add("sld.feeder.fuse", "elec.sld.feeder", "Feeder with fuse", "Abgang mit Sicherung", null, P(("device", "fuse")));
            Add("sld.meter", "elec.sld.meter", "Energy meter", "Energiezähler");

            // power electronics
            Add("pe.inverter", "elec.pe.inverter", "Inverter (3-phase)", "Wechselrichter (3-phasig)");
            Add("pe.rectifier", "elec.pe.rectifier", "Rectifier", "Gleichrichter");
            Add("pe.dcdc", "elec.pe.dcdc", "DC/DC converter", "DC/DC-Wandler");
            Add("pe.dclink", "elec.pe.dclink", "DC link capacitor", "Zwischenkreiskondensator");
            Add("pe.gatedriver", "elec.pe.gatedriver", "Gate driver", "Gate-Treiber");
            Add("pe.module", "elec.pe.module", "Power module (half bridge)", "Leistungsmodul (Halbbrücke)");

            // PCB
            Add("pcb.board", "elec.pcb.board", "Board outline", "Leiterplattenkontur");
            Add("pcb.pad", "elec.pcb.pad", "SMD pad", "SMD-Pad");
            Add("pcb.pad.tht", "elec.pcb.pad", "Through-hole pad", "THT-Pad", null, P(("shape", "round"), ("tht", true)), null, null, "tht", "through hole");
            Add("pcb.via", "elec.pcb.via", "Via", "Via");
            Add("pcb.track", "elec.pcb.track", "Track", "Leiterbahn");
            Add("pcb.copper", "elec.pcb.copper", "Copper area", "Kupferfläche");
            Add("pcb.soic8", "elec.pcb.footprint", "SOIC-8 footprint", "SOIC-8-Footprint", null, P(("package", "soic8")), null, new Size2(20, 16), "soic");
            Add("pcb.qfp32", "elec.pcb.footprint", "QFP-32 footprint", "QFP-32-Footprint", null, P(("package", "qfp32")), null, new Size2(24, 24), "qfp");
            Add("pcb.sot23", "elec.pcb.footprint", "SOT-23 footprint", "SOT-23-Footprint", null, P(("package", "sot23")), null, new Size2(12, 12), "sot");
            Add("pcb.r0603", "elec.pcb.footprint", "0603 chip footprint", "0603-Chip-Footprint", null, P(("package", "r0603")), null, new Size2(12, 6), "0603", "chip resistor");
            Add("pcb.dip8", "elec.pcb.footprint", "DIP-8 footprint", "DIP-8-Footprint", null, P(("package", "dip8")), null, new Size2(20, 16), "dip", "tht");
            Add("pcb.hole", "elec.pcb.hole", "Mounting hole", "Befestigungsbohrung");
            Add("pcb.silk", "elec.pcb.silk", "Silkscreen label", "Bestückungsdruck-Text", null, null, "U1");

            return list;
        }

        /// <summary>
        /// Connects an end point of a wire (or any point object) to the nearest pin (<see cref="AnchorRole.Port"/>) of a
        /// box object on the same page within <paramref name="tolerance"/> mm: the point is moved onto the pin and
        /// attached (normalised box coordinates plus the pin id), so it follows when the symbol is moved, rotated or scaled
        /// and, via <see cref="Attachments.Refresh"/>, when the pin moves inside the symbol (polarity flip, pin count).
        /// Returns the document unchanged if no pin is in reach.
        /// </summary>
        public static SketchDocument ConnectToPin(Registry registry, SketchDocument doc, string pointObjectId, int index, double tolerance = 2.0)
        {
            var wire = doc.Get(pointObjectId);
            var page = doc.PageOf(pointObjectId);
            if (wire?.Points is null || page is null || index < 0 || index >= wire.Points.Count) return doc;
            var at = doc.ResolvePoints(wire)[index];
            var ctx = new RenderContext(doc);
            SketchObject? best = null;
            Anchor? pin = null;
            var bestDist = tolerance;
            foreach (var o in doc.ObjectsOn(page))
            {
                if (o.Id == pointObjectId || o.IsPointGeometry || registry.Type(o.Type) is null) continue;
                foreach (var a in registry.PageAnchors(o, ctx))
                {
                    if (a.Kind != AnchorKind.Point || a.Role != AnchorRole.Port) continue;
                    var d = a.P.DistanceTo(at);
                    if (d <= bestDist) { bestDist = d; best = o; pin = a; }
                }
            }
            if (best is null || pin is null) return doc;
            return Edits.AttachPoint(doc, pointObjectId, index, best.Id, pin.P, pin.Id);
        }
    }
}
